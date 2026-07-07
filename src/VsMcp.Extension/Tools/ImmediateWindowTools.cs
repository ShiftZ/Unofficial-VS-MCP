using System.Threading.Tasks;
using EnvDTE;
using Newtonsoft.Json.Linq;
using VsMcp.Extension.McpServer;
using VsMcp.Extension.Services;
using VsMcp.Shared;
using VsMcp.Shared.Protocol;

namespace VsMcp.Extension.Tools
{
    public static class ImmediateWindowTools
    {
        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "immediate_execute",
                    "Execute an expression WITH side effects in the current debug context (currently selected thread and stack frame, like the VS Immediate Window) — assignments, mutating method calls, etc. Must be in break mode. For read-only inspection of a value without side effects, prefer debug_evaluate. To persist an expression that re-evaluates each break, use watch_add.",
                    SchemaBuilder.Create()
                        .AddString("expression", "The expression or statement to execute (e.g. 'myVar = 42', 'obj.Reset()')", required: true)
                        .AddInteger("timeout", "Evaluation timeout in milliseconds (default: 5000)")
                        .Build()),
                args => ImmediateExecuteAsync(accessor, args));
        }

        private static async Task<McpToolResult> ImmediateExecuteAsync(VsServiceAccessor accessor, JObject args)
        {
            var expression = args.Value<string>("expression");
            if (string.IsNullOrEmpty(expression))
                return McpToolResult.Error("Parameter 'expression' is required");

            var timeout = args.Value<int?>("timeout") ?? 5000;

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                    return McpToolResult.Error("Debugger must be in Break mode to execute expressions");

                var evaluation = DebugHelpers.TryEvaluateExpressionDetailed(dte.Debugger, expression, true, timeout);

                if (evaluation.Succeeded)
                {
                    var result = evaluation.Expression;
                    return McpToolResult.Success(new
                    {
                        expression,
                        value = result.Value,
                        type = result.Type
                    });
                }

                return McpToolResult.Error(
                    "Expression: " + expression + "\n" + evaluation.GetFailureSummary());
            });
        }
    }
}
