using System.Collections.Generic;
using EnvDTE;
using EnvDTE90a;

namespace VsMcp.Extension.Tools
{
    /// <summary>
    /// Shared helper methods for debug-related tools.
    /// </summary>
    internal static class DebugHelpers
    {
        // Localized forms of "Unknown" returned by VS for unrecognized frame languages
        private static readonly System.Collections.Generic.HashSet<string> UnknownLanguageNames =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "Unknown", "不明", "未知", "알 수 없음", "Unbekannt", "Inconnu", "Desconocido", "Sconosciuto", "Desconhecido", "Неизвестно", "Bilinmiyor", "Neznámý", "Nieznany" };

        public static Thread FindThread(Debugger debugger, int threadId)
        {
            foreach (Thread t in debugger.CurrentProgram.Threads)
            {
                try
                {
                    if (t.ID == threadId) return t;
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Determines if a stack frame is likely a managed code frame.
        /// Uses heuristics: known managed languages, or namespace-qualified function names.
        /// </summary>
        public static bool IsManagedFrame(StackFrame frame)
        {
            try
            {
                var lang = frame.Language;
                if (!string.IsNullOrEmpty(lang) && !UnknownLanguageNames.Contains(lang))
                    return true;

                var funcName = frame.FunctionName;
                if (string.IsNullOrEmpty(funcName))
                    return false;

                if (funcName.Length >= 8 && funcName[0] == '0' && funcName[1] == '0')
                    return false;

                if (funcName.StartsWith("["))
                    return false;

                if (funcName.Contains(".") && !funcName.Contains("\\"))
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Attempts to navigate to a managed stack frame in the current thread.
        /// Returns true if a managed frame was found and set as current.
        /// </summary>
        public static bool TryNavigateToManagedFrame(Debugger debugger)
        {
            try
            {
                var thread = debugger.CurrentThread;
                if (thread == null) return false;

                foreach (StackFrame frame in thread.StackFrames)
                {
                    try
                    {
                        if (IsManagedFrame(frame))
                        {
                            debugger.CurrentStackFrame = frame;
                            return true;
                        }
                    }
                    catch { }
                }

                foreach (Thread t in debugger.CurrentProgram.Threads)
                {
                    try
                    {
                        if (t.ID == thread.ID) continue;
                        foreach (StackFrame frame in t.StackFrames)
                        {
                            try
                            {
                                if (IsManagedFrame(frame))
                                {
                                    debugger.CurrentThread = t;
                                    debugger.CurrentStackFrame = frame;
                                    return true;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Tries to evaluate an expression in the current thread and stack frame.
        /// Returns the Expression result or null if evaluation failed in the current context.
        /// </summary>
        public static Expression TryEvaluateExpression(Debugger debugger, string expression)
        {
            return TryEvaluateExpression(debugger, expression, false, 3000);
        }

        public static Expression TryEvaluateExpression(Debugger debugger, string expression, bool allowSideEffects, int timeout)
        {
            var result = TryEvaluateExpressionDetailed(debugger, expression, allowSideEffects, timeout);
            return result.Succeeded ? result.Expression : null;
        }

        public static DebugExpressionEvaluationResult TryEvaluateExpressionDetailed(Debugger debugger, string expression)
        {
            return TryEvaluateExpressionDetailed(debugger, expression, false, 3000);
        }

        public static DebugExpressionEvaluationResult TryEvaluateExpressionDetailed(
            Debugger debugger,
            string expression,
            bool allowSideEffects,
            int timeout)
        {
            try
            {
                var context = CaptureCurrentEvaluationContext(debugger);
                var result = debugger.GetExpression(expression, allowSideEffects, timeout);
                if (result == null)
                    return DebugExpressionEvaluationResult.Failure(
                        context,
                        "Visual Studio returned no Expression object.",
                        null,
                        null);

                if (result.IsValidValue)
                    return DebugExpressionEvaluationResult.Success(context, result);

                return DebugExpressionEvaluationResult.Failure(
                    context,
                    "Visual Studio returned an invalid expression value.",
                    result,
                    null);
            }
            catch (System.Exception ex)
            {
                return DebugExpressionEvaluationResult.Failure(
                    CaptureCurrentEvaluationContext(debugger),
                    "Visual Studio threw while evaluating the expression.",
                    null,
                    ex);
            }
        }

        private static DebugExpressionEvaluationContext CaptureCurrentEvaluationContext(Debugger debugger)
        {
            var context = new DebugExpressionEvaluationContext();

            try
            {
                var thread = debugger.CurrentThread;
                if (thread == null)
                {
                    context.ThreadError = "No current thread is available.";
                }
                else
                {
                    context.ThreadId = thread.ID;
                    context.ThreadName = thread.Name;
                }
            }
            catch (System.Exception ex)
            {
                context.ThreadError = ex.Message;
            }

            try
            {
                var frame = debugger.CurrentStackFrame;
                if (frame == null)
                {
                    context.FrameError = "No current stack frame is available.";
                }
                else
                {
                    context.FunctionName = frame.FunctionName;
                    context.Module = frame.Module;
                    context.FileName = TryGetFrameFileName(frame);
                    context.Line = TryGetFrameLine(frame);
                    context.Language = frame.Language;
                }
            }
            catch (System.Exception ex)
            {
                context.FrameError = ex.Message;
            }

            return context;
        }

        public static string TryGetFrameFileName(StackFrame frame)
        {
            try
            {
                if (frame is StackFrame2 frame2) return frame2.FileName ?? "";
            }
            catch { return ""; }

            return "";
        }

        public static int TryGetFrameLine(StackFrame frame)
        {
            try
            {
                if (frame is StackFrame2 frame2) return checked((int)frame2.LineNumber);
            }
            catch { return 0; }

            return 0;
        }

        public static string TryGetThreadLocation(Thread thread)
        {
            try
            {
                var frames = thread.StackFrames;
                if (frames != null)
                {
                    foreach (StackFrame frame in frames)
                    {
                        return frame.FunctionName;
                    }
                }
            }
            catch { }
            return "";
        }
    }

    internal sealed class DebugExpressionEvaluationResult
    {
        private DebugExpressionEvaluationResult()
        {
        }

        public bool Succeeded { get; private set; }
        public Expression Expression { get; private set; }
        public DebugExpressionEvaluationContext Context { get; private set; }
        public string FailureReason { get; private set; }
        public string VisualStudioResult { get; private set; }
        public string ExpressionName { get; private set; }
        public string ExpressionType { get; private set; }
        public string ExceptionType { get; private set; }
        public string ExceptionMessage { get; private set; }

        public static DebugExpressionEvaluationResult Success(
            DebugExpressionEvaluationContext context,
            Expression expression)
        {
            return new DebugExpressionEvaluationResult
            {
                Succeeded = true,
                Expression = expression,
                Context = context
            };
        }

        public static DebugExpressionEvaluationResult Failure(
            DebugExpressionEvaluationContext context,
            string failureReason,
            Expression expression,
            System.Exception exception)
        {
            var result = new DebugExpressionEvaluationResult
            {
                Succeeded = false,
                Expression = expression,
                Context = context,
                FailureReason = failureReason,
                VisualStudioResult = TryGetExpressionValue(expression),
                ExpressionName = TryGetExpressionName(expression),
                ExpressionType = TryGetExpressionType(expression)
            };

            if (exception != null)
            {
                result.ExceptionType = exception.GetType().FullName;
                result.ExceptionMessage = exception.Message;
            }

            return result;
        }

        public string GetFailureSummary()
        {
            var parts = new List<string>
            {
                "Expression evaluation failed in the current thread and stack frame."
            };

            if (!string.IsNullOrEmpty(FailureReason))
                parts.Add("Reason: " + FailureReason);

            if (!string.IsNullOrEmpty(VisualStudioResult))
                parts.Add("Visual Studio result: " + VisualStudioResult);

            if (!string.IsNullOrEmpty(ExpressionName))
                parts.Add("Expression name: " + ExpressionName);

            if (!string.IsNullOrEmpty(ExpressionType))
                parts.Add("Expression type: " + ExpressionType);

            if (!string.IsNullOrEmpty(ExceptionMessage))
                parts.Add("Exception: " + ExceptionType + ": " + ExceptionMessage);

            if (Context != null)
                parts.Add("Context: " + Context.GetSummary());

            return string.Join("\n", parts);
        }

        private static string TryGetExpressionValue(Expression expression)
        {
            if (expression == null) return null;

            try { return expression.Value; }
            catch (System.Exception ex) { return "Could not read Expression.Value: " + ex.Message; }
        }

        private static string TryGetExpressionName(Expression expression)
        {
            if (expression == null) return null;

            try { return expression.Name; }
            catch { return null; }
        }

        private static string TryGetExpressionType(Expression expression)
        {
            if (expression == null) return null;

            try { return expression.Type; }
            catch { return null; }
        }
    }

    internal sealed class DebugExpressionEvaluationContext
    {
        public int? ThreadId { get; set; }
        public string ThreadName { get; set; }
        public string ThreadError { get; set; }
        public string FunctionName { get; set; }
        public string Module { get; set; }
        public string FileName { get; set; }
        public int Line { get; set; }
        public string Language { get; set; }
        public string FrameError { get; set; }

        public string GetSummary()
        {
            var parts = new List<string>();

            if (ThreadId.HasValue)
                parts.Add("threadId=" + ThreadId.Value);
            if (!string.IsNullOrEmpty(ThreadName))
                parts.Add("threadName=" + ThreadName);
            if (!string.IsNullOrEmpty(ThreadError))
                parts.Add("threadError=" + ThreadError);
            if (!string.IsNullOrEmpty(FunctionName))
                parts.Add("function=" + FunctionName);
            if (!string.IsNullOrEmpty(Language))
                parts.Add("language=" + Language);
            if (!string.IsNullOrEmpty(Module))
                parts.Add("module=" + Module);
            if (!string.IsNullOrEmpty(FileName))
                parts.Add("file=" + FileName);
            if (Line > 0)
                parts.Add("line=" + Line);
            if (!string.IsNullOrEmpty(FrameError))
                parts.Add("frameError=" + FrameError);

            return parts.Count == 0 ? "unavailable" : string.Join(", ", parts);
        }
    }
}
