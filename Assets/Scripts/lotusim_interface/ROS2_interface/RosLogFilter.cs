/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

// --------------------------------------------------------------------------------------------------------------------
//  RosLogFilter.cs
//
//  Description:
//  Throttles the "Connection to {ip}:{port} failed - ..." message that the ROS TCP Connector
//  (ROSConnection.ConnectionThread) logs once per second whenever the remote endpoint is
//  unreachable — which floods the console when the host is down or nothing is listening.
//
//  We install a decorating ILogHandler that forwards every log untouched EXCEPT this specific
//  spam, which is rate-limited to at most once every THROTTLE_MS so a genuine connection
//  failure is still visible without burying the rest of the console.
//
//  NOTE: the connector logs this from its background connection thread, so this handler must
//  never touch main-thread-only Unity APIs (e.g. Time.*). Environment.TickCount is thread-safe.
// --------------------------------------------------------------------------------------------------------------------

using System;
using UnityEngine;

namespace Lotusim
{
    public class RosLogFilter : ILogHandler
    {
        // Minimum gap between two printed connection-failure lines. Set very large to effectively mute.
        private const int THROTTLE_MS = 10000;

        private readonly ILogHandler m_inner;
        private int m_lastSpamTick; // 0 => first occurrence always prints (TickCount is uptime-ms, always >> THROTTLE_MS)

        private RosLogFilter(ILogHandler inner) => m_inner = inner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Guard against double-wrapping on repeated init (e.g. play mode without domain reload).
            if (Debug.unityLogger.logHandler is RosLogFilter)
                return;

            Debug.unityLogger.logHandler = new RosLogFilter(Debug.unityLogger.logHandler);
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            if (logType == LogType.Log && IsConnectionSpam(format, args))
            {
                int now = Environment.TickCount;
                if (unchecked(now - m_lastSpamTick) < THROTTLE_MS)
                    return; // within the throttle window — drop this repeat
                m_lastSpamTick = now;
            }

            m_inner.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            m_inner.LogException(exception, context);
        }

        // ROSConnection logs via Debug.Log(string) => format "{0}", args[0] = the message.
        // Match cheaply without allocating: only inspect args[0], fall back to format otherwise.
        private static bool IsConnectionSpam(string format, object[] args)
        {
            string msg = (args != null && args.Length == 1 && args[0] != null) ? args[0].ToString() : format;
            return msg != null
                && msg.StartsWith("Connection to ", StringComparison.Ordinal)
                && msg.Contains(" failed - ");
        }
    }
}
