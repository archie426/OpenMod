﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using SDG.Unturned;

namespace OpenMod.Unturned.Logging
{
    public class SerilogConsoleInputOutput : ICommandInputOutput
    {
        public event CommandInputHandler inputCommitted;

        private readonly IAutoCompleteHandler m_AutoCompleteHandler;
        private readonly ConcurrentQueue<string> m_CommandQueue;
        private readonly ILogger m_Logger;
        private Thread m_InputThread;
        private bool m_IsAlive;

        public SerilogConsoleInputOutput(
            ILoggerFactory loggerFactory,
            IAutoCompleteHandler autoCompleteHandler)
        {
            m_CommandQueue = new ConcurrentQueue<string>();
            m_AutoCompleteHandler = autoCompleteHandler;
            m_Logger = loggerFactory.CreateLogger("SDG.Unturned");
        }

        public void initialize(CommandWindow commandWindow)
        {
            if (m_IsAlive)
            {
                return;
            }

            ReadLine.AutoCompletionHandler = m_AutoCompleteHandler;
            m_IsAlive = true;
            m_InputThread = new Thread(OnInputThreadStart);
            m_InputThread.Start();
        }

        public void shutdown(CommandWindow commandWindow)
        {
            if (!m_IsAlive)
            {
                return;
            }

            m_IsAlive = false;
            ReadLine.AutoCompletionHandler = null;
        }

        public void update()
        {
            while (m_CommandQueue.TryDequeue(out var command))
            {
                inputCommitted?.Invoke(command); /* notify Unturned about inputted command */
            }
        }

        private void OnInputThreadStart()
        {
            while (m_IsAlive)
            {
                if (System.Console.KeyAvailable)
                {
                    string command = ReadLine.Read();
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        m_CommandQueue.Enqueue(command); /* Enqueue command because inputCommitted is expected on main thread */
                    }
                }

                Thread.Sleep(10);
            }
        }

        public void outputInformation(string information)
        {
            m_Logger.LogInformation(information);
        }

        public void outputWarning(string warning)
        {
            m_Logger.LogWarning(warning);
        }

        public void outputError(string error)
        {
            m_Logger.LogError(error);
        }
    }
}