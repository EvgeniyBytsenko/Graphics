﻿using System;
using System.Collections.Generic;
using System.Linq;
using VulkanSharp;
namespace Graphics.Engine.VulkanDriver
{
    internal class VulkanDevice
    {
       
        /// Видеоадаптер
        private PhysicalDevice _vulkanPhysicalDevice;

        /// Свойства и ограничения видеоадаптера
        private PhysicalDeviceProperties _vulkanPhysicalDeviceProperties;

        /// Параметры памяти: Типы памяти и кучи видеоадаптера
        private PhysicalDeviceMemoryProperties _vulkanPhysicalDeviceMemoryProperties;

        /// Возможности видеоадаптера, которые приложение может использовать
        private PhysicalDeviceFeatures _vulkanPhysicalDeviceFeatures;

        /// Представление видеоадаптера в виде логического устройства
        private Device _vulkanLogicalDevice;

        /// Требуемые пользователем возможности видеоадаптера при создании логического устройства
        /// Данные возможности должны входить в реальные возможности видеоадаптера
        /// определенные в поле <see cref="_vulkanPhysicalDeviceFeatures"/>
        private PhysicalDeviceFeatures _vulkanLogicalDeviceEnabledFeatures;
 
        /// Свойства семейств очередей видеоадаптера
        private List<QueueFamilyProperties> _queueFamilyProperties;

        /// Список имен расширений, которые поддерживает видеоадаптер
        private List<String> _supportedExtensions;

        /// Пул команд по умолчанию для семейства графических очередей
        private CommandPool _commandPool;

        /// Set to true when the debug marker extension is detected
        private Boolean _enableDebugMarkers = false;

        private struct QueueFamilyIndices
        {
            public UInt32 Graphics;
            public UInt32 Compute;
            public UInt32 Transfer;
        }

        /// <summary>
        /// При создании логического устройства, мы также задаем очередь или очереди для последующей работы с ними
        /// Обращение к этим очередям должно осуществляться по их индексам, данное поле как раз хранит эти индексы
        /// </summary>
        private QueueFamilyIndices _queueFamilyIndices;

        public VulkanDevice(PhysicalDevice physicalDevice,
            PhysicalDeviceFeatures requestedFeatures, 
            List<String> requestedExtensions, 
            Boolean useSwapChain = true,
            QueueFlags requestedQueueTypes = QueueFlags.Graphics | QueueFlags.Compute)
        {
            _queueFamilyIndices = new QueueFamilyIndices();
            _vulkanPhysicalDevice = physicalDevice;
            _vulkanPhysicalDeviceProperties = _vulkanPhysicalDevice.GetProperties();
            _vulkanPhysicalDeviceFeatures = _vulkanPhysicalDevice.GetFeatures();
            // Свойства памяти видеоадаптера, используются регулярно, для создания всех видов буферов
            _vulkanPhysicalDeviceMemoryProperties = _vulkanPhysicalDevice.GetMemoryProperties();
            // Свойства семейства очередей, используемые для настройки запрошенных очередей при создании устройства
            _queueFamilyProperties = new List<QueueFamilyProperties>();
            _queueFamilyProperties.AddRange(_vulkanPhysicalDevice.GetQueueFamilyProperties());
            _supportedExtensions = new List<String>();
            var extensions = _vulkanPhysicalDevice.EnumerateDeviceExtensionProperties(null);
            if (extensions.Length > 0)
            {
                foreach (var extensionProperties in extensions)
                {
                    _supportedExtensions.Add(extensionProperties.ExtensionName);
                }
            }
            CreateLogicalDevice(requestedFeatures, requestedExtensions, useSwapChain, requestedQueueTypes);
        }

        /// <summary>
        /// Возвращает индекс семейства очередей. Очередь должна поддерживать флаги, которые указаны <paramref name="queueFlags"/>
        /// На самом же деле флаг обычно указывается один, например: QueueFlags.Graphics или QueueFlags.Compute
        /// Но в силу того, что бывают очереди, которые поддерживают несколько флагов одновременно (многофункциональные очереди)
        /// и тут же есть очереди узконаправленные (очередь выполняет только одну функцию), 
        /// то поиск ведется сначала среди специально ориентированных, а затем многофункциональных
        /// </summary>
        /// <param name="queueFlags">Флаги, которым должно удовлетворять семейство очередей</param>
        /// <returns>Индекс подходящего семейства очередей</returns>
        public UInt32 GetQueueFamilyIndex(QueueFlags queueFlags)
        {
            var isComputeQueue = (queueFlags & QueueFlags.Compute) == QueueFlags.Compute &&
                                 ((queueFlags & QueueFlags.Graphics) == 0 &&
                                  (queueFlags & QueueFlags.Transfer) == 0 &&
                                  (queueFlags & QueueFlags.SparseBinding) == 0);

            var isGraphicsQueue = (queueFlags & QueueFlags.Graphics) == QueueFlags.Graphics &&
                                  ((queueFlags & QueueFlags.Compute) == 0 &&
                                   (queueFlags & QueueFlags.Transfer) == 0 &&
                                   (queueFlags & QueueFlags.SparseBinding) == 0);

            var isTransferQueue = (queueFlags & QueueFlags.Transfer) == QueueFlags.Transfer &&
                                  ((queueFlags & QueueFlags.Compute) == 0 &&
                                   (queueFlags & QueueFlags.Graphics) == 0 &&
                                   (queueFlags & QueueFlags.SparseBinding) == 0);

            if ((queueFlags & QueueFlags.Compute) == QueueFlags.Compute)
            {
                if (isComputeQueue)
                {
                    for (var i = 0; i < _queueFamilyProperties.Count; i++)
                    {
                        if ((_queueFamilyProperties[i].QueueFlags & QueueFlags.Compute) ==
                            QueueFlags.Compute &&
                            ((_queueFamilyProperties[i].QueueFlags & QueueFlags.Graphics) == 0 &&
                             (_queueFamilyProperties[i].QueueFlags & QueueFlags.Transfer) == 0 &&
                             (_queueFamilyProperties[i].QueueFlags & QueueFlags.SparseBinding) == 0))
                        {
                            return (UInt32) i;
                        }
                    }
                }
                for (var i = 0; i < _queueFamilyProperties.Count; i++)
                {
                    if ((_queueFamilyProperties[i].QueueFlags & queueFlags) == queueFlags &&
                        (_queueFamilyProperties[i].QueueFlags & QueueFlags.Graphics) == 0)
                    {
                        return (UInt32) i;
                    }
                }
            }
            if ((queueFlags & QueueFlags.Transfer) == QueueFlags.Transfer)
            {
                if (isTransferQueue)
                {
                    for (var i = 0; i < _queueFamilyProperties.Count; i++)
                    {
                        if ((_queueFamilyProperties[i].QueueFlags & QueueFlags.Transfer) ==
                            QueueFlags.Transfer &&
                            ((_queueFamilyProperties[i].QueueFlags & QueueFlags.Graphics) == 0 &&
                             (_queueFamilyProperties[i].QueueFlags & QueueFlags.Compute) == 0 &&
                             (_queueFamilyProperties[i].QueueFlags & QueueFlags.SparseBinding) == 0))
                        {
                            return (UInt32) i;
                        }
                    }
                }
                for (var i = 0; i < _queueFamilyProperties.Count; i++)
                {
                    if ((_queueFamilyProperties[i].QueueFlags & queueFlags) == queueFlags &&
                        (_queueFamilyProperties[i].QueueFlags & QueueFlags.Graphics) == 0 &&
                        (_queueFamilyProperties[i].QueueFlags & QueueFlags.Compute) == 0)
                    {
                        return (UInt32) i;
                    }
                }
            }
            if ((queueFlags & QueueFlags.Graphics) == QueueFlags.Graphics)
            {
                if (isGraphicsQueue)
                {
                    for (var i = 0; i < _queueFamilyProperties.Count; i++)
                    {
                        if ((_queueFamilyProperties[i].QueueFlags & QueueFlags.Graphics) ==
                            QueueFlags.Graphics &&
                            ((_queueFamilyProperties[i].QueueFlags & QueueFlags.Transfer) == 0 &&
                             (_queueFamilyProperties[i].QueueFlags & QueueFlags.Compute) == 0 &&
                             (_queueFamilyProperties[i].QueueFlags & QueueFlags.SparseBinding) == 0))
                        {
                            return (UInt32) i;
                        }
                    }
                }
            }
            for (var i = 0; i < _queueFamilyProperties.Count; i++)
            {
                if ((_queueFamilyProperties[i].QueueFlags & queueFlags) == queueFlags)
                {
                    return (UInt32) i;
                }
            }
            throw new Exception("Не удалось найти подходящий индекс семества очередей");
        }

        /// <summary>
        /// Проверка, поддерживается ли расширение, которое указано <paramref name="extensionName"/> видеоадаптером
        /// </summary>
        /// <param name="extensionName">Имя расширения</param>
        /// <returns>True - если расширение поддерживается (Присутствует в списке, читаемом во время создания устройства)</returns>
        public Boolean ExtensionSupported(String extensionName)
        {
            var support = _supportedExtensions.FirstOrDefault(e => e == extensionName);
            return support != null;
        }

        /** 
		* Create a command pool for allocation command buffers from
		* 
		* @param queueFamilyIndex Family index of the queue to create the command pool for
		* @param createFlags (Optional) Command pool creation flags (Defaults to VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT)
		*
		* @note Command buffers allocated from the created pool can only be submitted to a queue with the same family index
		*
		* @return A handle to the created command buffer
		*/
        public CommandPool CreateCommandPool(UInt32 queueFamilyIndex,
            CommandPoolCreateFlags createFlags = CommandPoolCreateFlags.ResetCommandBuffer)
        {
            var cmdPoolInfo = new CommandPoolCreateInfo
            {
                QueueFamilyIndex = queueFamilyIndex,
                Flags = createFlags
            };
            var cmdPool = _vulkanLogicalDevice.CreateCommandPool(cmdPoolInfo);
            return cmdPool;
        }

        private void CreateLogicalDevice(PhysicalDeviceFeatures requestedFeatures,
            IEnumerable<String> requestedExtensions, Boolean useSwapChain, QueueFlags requestedQueueTypes)
        {
            // Desired queues need to be requested upon logical device creation
            // Due to differing queue family configurations of Vulkan implementations this can be a bit tricky, especially if the application
            // requests different queue types

            var queueCreateInfos = new List<DeviceQueueCreateInfo>();

            // Get queue family indices for the requested queue family types
            // Note that the indices may overlap depending on the implementation

            const Single defaultQueuePriority = 0.0f;

            // Очередь для отрисовки графики
            if ((requestedQueueTypes & QueueFlags.Graphics) == QueueFlags.Graphics)
            {
                _queueFamilyIndices.Graphics = GetQueueFamilyIndex(QueueFlags.Graphics);
                var queueInfo = new DeviceQueueCreateInfo
                {
                    QueueFamilyIndex = _queueFamilyIndices.Graphics,
                    QueueCount = 1,
                    QueuePriorities = new Single[1] {defaultQueuePriority}
                };
                queueCreateInfos.Add(queueInfo);
            }
            else
            {
                _queueFamilyIndices.Graphics = 0;
            }

            // Выделенная очередь для вычислений
            if ((requestedQueueTypes & QueueFlags.Compute) == QueueFlags.Compute)
            {
                _queueFamilyIndices.Compute = GetQueueFamilyIndex(QueueFlags.Compute);
                if (_queueFamilyIndices.Compute != _queueFamilyIndices.Graphics)
                {
                    // Если индекс вычисляемого семейства очередей отличается от графического семейства,
                    // то нам нужна дополнительная очередь для вычислений
                    var queueInfo = new DeviceQueueCreateInfo
                    {
                        QueueFamilyIndex = _queueFamilyIndices.Compute,
                        QueueCount = 1,
                        QueuePriorities = new Single[1] { defaultQueuePriority }
                    };
                    queueCreateInfos.Add(queueInfo);
                }
            }
            else
            {
                // Иначе используем индекc графичесвой очереди
                _queueFamilyIndices.Compute = _queueFamilyIndices.Graphics;
            }

            // Выделенная очередь для передачи
            if ((requestedQueueTypes & QueueFlags.Transfer) == QueueFlags.Transfer)
            {
                _queueFamilyIndices.Transfer = GetQueueFamilyIndex(QueueFlags.Transfer);
                if ((_queueFamilyIndices.Transfer != _queueFamilyIndices.Graphics) 
                    && (_queueFamilyIndices.Transfer != _queueFamilyIndices.Compute))
                {
                    // If compute family index differs, we need an additional queue create info for the compute queue
                    // Если индекс семейства очередей для передачи отличается от графического и вычисляемого семейства,
                    // то нам нужна дополнительная очередь для передачи
                    var queueInfo = new DeviceQueueCreateInfo
                    {
                        QueueFamilyIndex = _queueFamilyIndices.Transfer,
                        QueueCount = 1,
                        QueuePriorities = new Single[1] { defaultQueuePriority }
                    };
                    queueCreateInfos.Add(queueInfo);
                }
            }
            else
            {
                // Иначе используем индекc графичесвой очереди
                _queueFamilyIndices.Transfer = _queueFamilyIndices.Graphics;
            }

            // Create the logical device representation
            var deviceExtensions = new List<String>(requestedExtensions);
            if (useSwapChain)
            {
                // If the device will be used for presenting to a display via a swapchain we need to request the swapchain extension
                deviceExtensions.Add("VK_KHR_swapchain");
            }

            var deviceCreateInfo = new DeviceCreateInfo
            {
                QueueCreateInfoCount = (UInt32) queueCreateInfos.Count,
                QueueCreateInfos = queueCreateInfos.ToArray(),
                EnabledFeatures = requestedFeatures
            };

            // Enable the debug marker extension if it is present (likely meaning a debugging tool is present)
            if (ExtensionSupported("VK_EXT_debug_marker"))
            {
                deviceExtensions.Add("VK_EXT_debug_marker");
                _enableDebugMarkers = true;
            }

            if (deviceExtensions.Count > 0)
            {
                deviceCreateInfo.EnabledExtensionCount = (UInt32)deviceExtensions.Count;
                deviceCreateInfo.EnabledExtensionNames = deviceExtensions.ToArray();
            }

            _vulkanLogicalDevice = _vulkanPhysicalDevice.CreateDevice(deviceCreateInfo);

            _commandPool = CreateCommandPool(_queueFamilyIndices.Graphics);

            _vulkanLogicalDeviceEnabledFeatures = requestedFeatures;

            _vulkanLogicalDevice.Destroy();
        }
    }
}