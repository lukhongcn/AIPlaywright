using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using ModuleWorkFlow.AIHelp.Config;
using ModuleWorkFlow.PlaywrightRunner.Infrastructure;

namespace ModuleWorkFlow.PlaywrightRunner.Actions.PartProcess
{
    internal static class PartProcessMoveDryRunRunner
    {
        private const string ActionName = "part_process_move_dryrun";
        private const string EditPath = "/PartModifyEditall.aspx";

        public static async Task<PartProcessMoveDryRunResult> RunAsync(IPage page, PartProcessMoveDryRunRequest request)
        {
            request = request ?? new PartProcessMoveDryRunRequest();

            var result = new PartProcessMoveDryRunResult
            {
                Action = ActionName,
                Executed = false,
                Success = false,
                Status = "failed",
                Message = "無法進入零件工藝編輯頁。",
                Saved = false
            };

            try
            {
                NormalizeRequest(request);
                if (page == null)
                {
                    result.Message = "缺少可用的瀏覽器頁面。";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(request.BaseUrl))
                {
                    result.Message = "缺少 Mes.BaseUrl，無法打開零件工藝編輯頁。";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(request.ModuleId))
                {
                    result.Message = "缺少 moduleId，無法打開零件工藝編輯頁。";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(request.PartNoList))
                {
                    result.Message = "缺少 partNoList 或 partNo，無法打開零件工藝編輯頁。";
                    return result;
                }

                var editUrl = BuildEditUrl(request);
                await page.GotoAsync(editUrl);
                await WaitForGridReadyAsync(page);

                var before = await ReadProcessesAsync(page);
                result.BeforeProcesses = before;
                result.AfterProcesses = before;
                result.CurrentUrl = page.Url;
                result.Executed = true;

                if (before.Count == 0)
                {
                    result.Status = "empty_process_list";
                    result.Message = "已進入零件工藝編輯頁，但找不到工序列表。";
                    result.ScreenshotPath = await TryTakeScreenshotAsync(page, "part_process_move_empty");
                    return result;
                }

                var resolved = ResolveMoveTarget(request, before);
                if (!resolved.Success)
                {
                    result.Success = resolved.ReturnSuccess;
                    result.Status = resolved.Status;
                    result.Message = resolved.Message;
                    result.FromPosition = resolved.FromPosition;
                    result.ToPosition = resolved.ToPosition;
                    result.Direction = resolved.Direction;
                    result.Steps = resolved.Steps;
                    result.ScreenshotPath = await TryTakeScreenshotAsync(page, "part_process_move_opened");
                    return result;
                }

                result.FromPosition = resolved.FromPosition;
                result.ToPosition = resolved.ToPosition;
                result.Direction = resolved.Direction;
                result.Steps = resolved.Steps;

                var currentPosition = resolved.FromPosition.Value;
                for (var i = 0; i < resolved.Steps; i++)
                {
                    var direction = resolved.Direction;
                    var oldSignature = BuildSignature(result.AfterProcesses);
                    var row = await GetDataRowLocatorAsync(page, currentPosition);
                    if (row == null)
                    {
                        result.Success = false;
                        result.Status = "row_not_found";
                        result.Message = "重新定位工序行失敗。";
                        result.ScreenshotPath = await TryTakeScreenshotAsync(page, "part_process_move_error");
                        return result;
                    }

                    if (string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase))
                    {
                        await ClickRowButtonAsync(page, row, "上移", "move-process-up");
                        currentPosition--;
                    }
                    else
                    {
                        await ClickRowButtonAsync(page, row, "下移", "move-process-down");
                        currentPosition++;
                    }

                    await WaitForGridChangedAsync(page, oldSignature);
                    result.AfterProcesses = await ReadProcessesAsync(page);
                }

                result.CurrentUrl = page.Url;
                result.Success = true;
                result.Status = "moved";
                result.Message = "已完成工序順序 dry-run 調整，尚未保存。";
                result.ScreenshotPath = await TryTakeScreenshotAsync(page, "part_process_move_done");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Status = "error";
                result.Message = ex.Message;
                result.CurrentUrl = page == null ? string.Empty : page.Url;
                result.ScreenshotPath = page == null ? null : await TryTakeScreenshotAsync(page, "part_process_move_error");
                return result;
            }
        }

        private static void NormalizeRequest(PartProcessMoveDryRunRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BaseUrl))
            {
                var config = AppConfig.Load();
                if (config != null && config.Mes != null)
                {
                    request.BaseUrl = config.Mes.BaseUrl;
                }
            }

            request.BaseUrl = NormalizeValue(request.BaseUrl);
            request.ModuleId = NormalizeValue(request.ModuleId);
            request.PartNo = NormalizeValue(request.PartNo);
            request.PartNoList = NormalizeValue(request.PartNoList);
            request.MenuId = NormalizeValue(request.MenuId) ?? "F10";
            request.PageIndex = NormalizeValue(request.PageIndex) ?? "1";
            request.ListUrl = NormalizeValue(request.ListUrl) ?? "PartList.aspx";
            request.Direction = NormalizeValue(request.Direction);
            request.ProcessName = NormalizeValue(request.ProcessName);

            if (string.IsNullOrWhiteSpace(request.PartNoList) && !string.IsNullOrWhiteSpace(request.PartNo))
            {
                request.PartNoList = request.PartNo;
            }

            if (request.Steps <= 0)
            {
                request.Steps = 1;
            }
        }

        private static string NormalizeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string BuildEditUrl(PartProcessMoveDryRunRequest request)
        {
            return request.BaseUrl.TrimEnd('/') +
                   EditPath +
                   "?menuid=" + Uri.EscapeDataString(request.MenuId) +
                   "&moduleid=" + Uri.EscapeDataString(request.ModuleId) +
                   "&PartNolist=" + Uri.EscapeDataString(request.PartNoList) +
                   "&pageIndex=" + Uri.EscapeDataString(request.PageIndex) +
                   "&listurl=" + Uri.EscapeDataString(request.ListUrl);
        }

        private static MovePlan ResolveMoveTarget(PartProcessMoveDryRunRequest request, List<PartProcessItem> processes)
        {
            var plan = new MovePlan
            {
                ReturnSuccess = true,
                Status = "edit_page_opened",
                Message = "已進入零件工藝編輯頁，請手動調整工序順序",
                Steps = request.Steps
            };

            var fromPosition = request.FromPosition;
            if (!string.IsNullOrWhiteSpace(request.ProcessName))
            {
                var matches = processes
                    .Where(m => string.Equals(m.ProcessName ?? string.Empty, request.ProcessName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                {
                    plan.Status = "process_not_found";
                    plan.Message = "找不到指定工序。";
                    return plan;
                }

                if (matches.Count > 1)
                {
                    plan.Status = "multiple_processes_found";
                    plan.Message = "找到多個同名工序，請指定第幾道。";
                    return plan;
                }

                fromPosition = matches[0].Position;
            }

            var hasDirection = !string.IsNullOrWhiteSpace(request.Direction);
            var hasTarget = request.ToPosition.HasValue;
            if (!fromPosition.HasValue || (!hasDirection && !hasTarget))
            {
                return plan;
            }

            if (fromPosition.Value < 1 || fromPosition.Value > processes.Count)
            {
                plan.ReturnSuccess = false;
                plan.Status = "invalid_from_position";
                plan.Message = "fromPosition 超出工序範圍。";
                plan.FromPosition = fromPosition;
                return plan;
            }

            if (hasTarget)
            {
                if (request.ToPosition.Value < 1 || request.ToPosition.Value > processes.Count)
                {
                    plan.ReturnSuccess = false;
                    plan.Status = "invalid_target_position";
                    plan.Message = "toPosition 超出工序範圍。";
                    plan.FromPosition = fromPosition;
                    plan.ToPosition = request.ToPosition;
                    return plan;
                }

                if (fromPosition.Value == request.ToPosition.Value)
                {
                    plan.Status = "no_change";
                    plan.Message = "fromPosition 與 toPosition 相同，未執行移動。";
                    plan.FromPosition = fromPosition;
                    plan.ToPosition = request.ToPosition;
                    return plan;
                }

                plan.Success = true;
                plan.FromPosition = fromPosition;
                plan.ToPosition = request.ToPosition;
                plan.Direction = fromPosition.Value > request.ToPosition.Value ? "up" : "down";
                plan.Steps = Math.Abs(fromPosition.Value - request.ToPosition.Value);
                return plan;
            }

            var direction = request.Direction.Trim().ToLowerInvariant();
            if (!direction.Equals("up") && !direction.Equals("down"))
            {
                plan.ReturnSuccess = false;
                plan.Status = "invalid_direction";
                plan.Message = "direction 只允許 up 或 down。";
                plan.FromPosition = fromPosition;
                return plan;
            }

            if (direction.Equals("up") && fromPosition.Value == 1)
            {
                plan.Status = "already_first";
                plan.Message = "第一道工序不能再上移。";
                plan.FromPosition = fromPosition;
                plan.Direction = direction;
                return plan;
            }

            if (direction.Equals("down") && fromPosition.Value == processes.Count)
            {
                plan.Status = "already_last";
                plan.Message = "最後一道工序不能再下移。";
                plan.FromPosition = fromPosition;
                plan.Direction = direction;
                return plan;
            }

            if (direction.Equals("up") && fromPosition.Value - request.Steps < 1)
            {
                plan.Status = "already_first";
                plan.Message = "目標位置已超過第一道，未執行移動。";
                plan.FromPosition = fromPosition;
                plan.Direction = direction;
                plan.Steps = request.Steps;
                return plan;
            }

            if (direction.Equals("down") && fromPosition.Value + request.Steps > processes.Count)
            {
                plan.Status = "already_last";
                plan.Message = "目標位置已超過最後一道，未執行移動。";
                plan.FromPosition = fromPosition;
                plan.Direction = direction;
                plan.Steps = request.Steps;
                return plan;
            }

            plan.Success = true;
            plan.FromPosition = fromPosition;
            plan.Direction = direction;
            plan.Steps = request.Steps;
            plan.ToPosition = direction.Equals("up")
                ? fromPosition.Value - request.Steps
                : fromPosition.Value + request.Steps;
            return plan;
        }

        private static async Task WaitForGridReadyAsync(IPage page)
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.Locator("table[id$='MainDataGrid']").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 15000
            });

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                {
                    Timeout = 3000
                });
            }
            catch
            {
            }
        }

        private static async Task<List<PartProcessItem>> ReadProcessesAsync(IPage page)
        {
            var list = new List<PartProcessItem>();
            var rows = page.Locator("table[id$='MainDataGrid'] tr");
            var count = await rows.CountAsync();
            for (var i = 0; i < count; i++)
            {
                var row = rows.Nth(i);
                var processLocator = row.Locator("[id$='Label_CustomerProcessName']");
                if (await processLocator.CountAsync() == 0)
                {
                    continue;
                }

                var processName = (await processLocator.First.InnerTextAsync()).Trim();
                if (string.IsNullOrWhiteSpace(processName))
                {
                    continue;
                }

                var orderLocator = row.Locator("[id$='Label_OrderNo']");
                var orderNo = await orderLocator.CountAsync() > 0
                    ? (await orderLocator.First.InnerTextAsync()).Trim()
                    : string.Empty;

                list.Add(new PartProcessItem
                {
                    Position = list.Count + 1,
                    OrderNo = orderNo,
                    ProcessName = processName
                });
            }

            return list;
        }

        private static async Task<ILocator> GetDataRowLocatorAsync(IPage page, int position)
        {
            var rows = page.Locator("table[id$='MainDataGrid'] tr");
            var count = await rows.CountAsync();
            var current = 0;
            for (var i = 0; i < count; i++)
            {
                var row = rows.Nth(i);
                var processLocator = row.Locator("[id$='Label_CustomerProcessName']");
                if (await processLocator.CountAsync() == 0)
                {
                    continue;
                }

                var processName = (await processLocator.First.InnerTextAsync()).Trim();
                if (string.IsNullOrWhiteSpace(processName))
                {
                    continue;
                }

                current++;
                if (current == position)
                {
                    return row;
                }
            }

            return null;
        }

        private static async Task ClickRowButtonAsync(IPage page, ILocator row, string text, string purpose)
        {
            var candidates = new[]
            {
                row.Locator("input[type='submit'][value='" + text + "']"),
                row.Locator("input[type='button'][value='" + text + "']"),
                row.Locator("button:has-text('" + text + "')"),
                row.Locator("a:has-text('" + text + "')")
            };

            foreach (var locator in candidates)
            {
                if (await locator.CountAsync() == 0)
                {
                    continue;
                }

                var button = locator.First;
                if (await button.IsVisibleAsync() && await button.IsEnabledAsync())
                {
                    await SafeClickHelper.SafeClickAsync(page, button, purpose);
                    return;
                }
            }

            throw new InvalidOperationException("找不到可點擊的「" + text + "」按鈕。");
        }

        private static async Task WaitForGridChangedAsync(IPage page, string oldSignature)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < 8)
            {
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                    {
                        Timeout = 500
                    });
                }
                catch
                {
                }

                await Task.Delay(250);
                var current = await ReadProcessesAsync(page);
                var signature = BuildSignature(current);
                if (!string.Equals(signature, oldSignature, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        private static string BuildSignature(List<PartProcessItem> items)
        {
            return string.Join("|", items.Select(m => (m.Position + ":" + (m.OrderNo ?? string.Empty) + ":" + (m.ProcessName ?? string.Empty)).Trim()));
        }

        private static async Task<string> TryTakeScreenshotAsync(IPage page, string prefix)
        {
            if (page == null)
            {
                return null;
            }

            try
            {
                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
                Directory.CreateDirectory(directory);
                var fileName = prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                var path = Path.Combine(directory, fileName);
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = path,
                    FullPage = true
                });
                return path;
            }
            catch
            {
                return null;
            }
        }

        private sealed class MovePlan
        {
            public bool Success { get; set; }

            public bool ReturnSuccess { get; set; }

            public string Status { get; set; }

            public string Message { get; set; }

            public int? FromPosition { get; set; }

            public int? ToPosition { get; set; }

            public string Direction { get; set; }

            public int Steps { get; set; }
        }
    }
}
