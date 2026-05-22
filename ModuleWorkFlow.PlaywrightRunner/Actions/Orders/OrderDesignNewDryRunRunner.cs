using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using ModuleWorkFlow.AIHelp.Config;
using ModuleWorkFlow.PlaywrightRunner.Infrastructure;

namespace ModuleWorkFlow.PlaywrightRunner.Actions.Orders
{
    internal static class OrderDesignNewDryRunRunner
    {
        private const string ActionName = "order_design_new_dryrun";
        private const string ListPath = "/order/OrderDesignList.aspx";
        private const string AddPageKeyword = "OrderDesignView.aspx";

        public static async Task<OrderDesignActionResult> RunAsync(OrderDesignNewDryRunRequest request)
        {
            request = request ?? new OrderDesignNewDryRunRequest();

            var result = new OrderDesignActionResult
            {
                Action = ActionName,
                Executed = false,
                Success = false,
                Status = "failed",
                Message = "無法進入訂單新增頁。"
            };

            IPlaywright playwright = null;
            IBrowser browser = null;
            IPage page = null;

            try
            {
                NormalizeRequest(request);
                if (string.IsNullOrWhiteSpace(request.BaseUrl))
                {
                    result.Status = "failed";
                    result.Message = "缺少 Mes.BaseUrl，無法打開訂單設計頁。";
                    return result;
                }

                playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Channel = "msedge",
                    Headless = false,
                    SlowMo = 300
                });
                page = await browser.NewPageAsync();
                return await RunAsync(page, request);
            }
            catch (Exception ex)
            {
                result.Executed = true;
                result.Success = false;
                result.Status = "error";
                result.Message = ex.Message;
                result.CurrentUrl = page == null ? string.Empty : page.Url;
                result.PageTitle = page == null ? string.Empty : await TryGetTitleAsync(page);
                result.ScreenshotPath = page == null ? null : await TryTakeScreenshotAsync(page, "order_design_new_error");
                return result;
            }
            finally
            {
                if (browser != null)
                {
                    await browser.CloseAsync();
                }

                if (playwright != null)
                {
                    playwright.Dispose();
                }
            }
        }

        public static async Task<OrderDesignActionResult> RunAsync(IPage page, OrderDesignNewDryRunRequest request)
        {
            request = request ?? new OrderDesignNewDryRunRequest();

            var result = new OrderDesignActionResult
            {
                Action = ActionName,
                Executed = false,
                Success = false,
                Status = "failed",
                Message = "無法進入訂單新增頁。"
            };

            try
            {
                NormalizeRequest(request);
                if (page == null)
                {
                    result.Status = "failed";
                    result.Message = "缺少可用的瀏覽器頁面。";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(request.BaseUrl))
                {
                    result.Status = "failed";
                    result.Message = "缺少 Mes.BaseUrl，無法打開訂單設計頁。";
                    return result;
                }

                var listUrl = request.BaseUrl.TrimEnd('/') + ListPath;
                result.Data["listUrl"] = listUrl;
                await page.GotoAsync(listUrl);
                await WaitForPageStableAsync(page);

                var addClick = await ClickAddButtonAsync(page);
                result.Data["addButtonSelector"] = addClick.Selector;
                result.Data["addButtonText"] = addClick.Text;

                var enteredAddPage = await WaitForAddPageAsync(page);
                if (!enteredAddPage)
                {
                    result.Executed = true;
                    result.CurrentUrl = page.Url;
                    result.PageTitle = await page.TitleAsync();
                    result.Data["pageMessage"] = await ReadMessageAsync(page);
                    result.ScreenshotPath = await TryTakeScreenshotAsync(page, "order_design_new_failed");
                    return result;
                }

                var fillResults = new Dictionary<string, object>();
                var missingFields = new List<string>();
                var filledFields = new List<string>();

                await FillOptionalFieldAsync(page, "customer", request.Customer, fillResults, filledFields, missingFields, GetCustomerSelectors());
                await FillOptionalFieldAsync(page, "itemNo", request.ItemNo, fillResults, filledFields, missingFields, GetItemNoSelectors());
                await FillOptionalFieldAsync(page, "quantity", request.Quantity, fillResults, filledFields, missingFields, GetQuantitySelectors());
                await FillOptionalFieldAsync(page, "orderDate", request.OrderDate, fillResults, filledFields, missingFields, GetOrderDateSelectors());
                await FillOptionalFieldAsync(page, "deliveryDate", request.DeliveryDate, fillResults, filledFields, missingFields, GetDeliveryDateSelectors());
                await FillOptionalFieldAsync(page, "remark", request.Remark, fillResults, filledFields, missingFields, GetRemarkSelectors());

                result.Executed = true;
                result.Success = true;
                result.CurrentUrl = page.Url;
                result.PageTitle = await page.TitleAsync();
                result.ScreenshotPath = await TryTakeScreenshotAsync(page, "order_design_new_prepared");
                result.Data["pageMessage"] = await ReadMessageAsync(page);
                result.Data["filledFields"] = filledFields;
                result.Data["missingFields"] = missingFields;
                result.Data["fieldResults"] = fillResults;

                if (missingFields.Count == 0)
                {
                    result.Status = "prepared";
                    result.Message = "訂單資料已填好，請在 MES 頁面檢查。確認無誤後，請手動點保存。";
                }
                else
                {
                    result.Status = "partial_prepared";
                    result.Message = "新增頁已打開，但部分欄位未找到，請人工檢查後再保存。";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Executed = true;
                result.Success = false;
                result.Status = "error";
                result.Message = ex.Message;
                result.CurrentUrl = page == null ? string.Empty : page.Url;
                result.PageTitle = page == null ? string.Empty : await TryGetTitleAsync(page);
                result.ScreenshotPath = page == null ? null : await TryTakeScreenshotAsync(page, "order_design_new_error");
                return result;
            }
        }

        private static void NormalizeRequest(OrderDesignNewDryRunRequest request)
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
            request.Customer = NormalizeValue(request.Customer);
            request.ItemNo = NormalizeValue(request.ItemNo);
            request.Quantity = NormalizeValue(request.Quantity);
            request.OrderDate = NormalizeValue(request.OrderDate);
            request.DeliveryDate = NormalizeValue(request.DeliveryDate);
            request.Remark = NormalizeValue(request.Remark);
        }

        private static string NormalizeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static async Task<ClickResult> ClickAddButtonAsync(IPage page)
        {
            var selectors = new[]
            {
                "a[id$='lnkbutton_add']",
                "input[id$='lnkbutton_add']",
                "a[title*='新增']",
                "a[title*='add']",
                "input[value='新增']",
                "input[value='新建']",
                "input[value='Add']",
                "input[value='New']",
                "button:has-text('新增')",
                "button:has-text('新建')",
                "a:has-text('新增')",
                "a:has-text('新建')",
                "a:has-text('Add')",
                "a:has-text('New')"
            };

            foreach (var selector in selectors)
            {
                var locator = await FindFirstActionableAsync(page, selector);
                if (locator == null)
                {
                    continue;
                }

                var text = await ReadLocatorTextAsync(locator);
                await SafeClickHelper.SafeClickAsync(page, locator, "open-order-design-add-page");
                return new ClickResult
                {
                    Selector = selector,
                    Text = text
                };
            }

            throw new InvalidOperationException("找不到可用的新增按鈕。");
        }

        private static async Task<bool> WaitForAddPageAsync(IPage page)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < 15)
            {
                await WaitForPageStableAsync(page);

                if (page.Url.IndexOf(AddPageKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (await HasVisibleLocatorAsync(page, "table[id$='Table4']") ||
                    await HasVisibleLocatorAsync(page, "text=儲存/save") ||
                    await HasVisibleLocatorAsync(page, "a[href='OrderDesignView.aspx']"))
                {
                    return true;
                }

                await Task.Delay(300);
            }

            return false;
        }

        private static async Task FillOptionalFieldAsync(
            IPage page,
            string fieldName,
            string value,
            Dictionary<string, object> fieldResults,
            List<string> filledFields,
            List<string> missingFields,
            string[] selectors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                fieldResults[fieldName] = new Dictionary<string, object>
                {
                    { "requested", false },
                    { "filled", false },
                    { "reason", "empty_input" }
                };
                return;
            }

            var fillResult = await TryFillFirstAvailableAsync(page, selectors, value);
            fieldResults[fieldName] = new Dictionary<string, object>
            {
                { "requested", true },
                { "filled", fillResult.Success },
                { "selector", fillResult.Selector ?? string.Empty },
                { "controlType", fillResult.ControlType ?? string.Empty },
                { "actualValue", fillResult.ActualValue ?? string.Empty }
            };

            if (fillResult.Success)
            {
                filledFields.Add(fieldName);
            }
            else
            {
                missingFields.Add(fieldName);
            }
        }

        private static async Task<FillResult> TryFillFirstAvailableAsync(IPage page, string[] selectors, string value)
        {
            foreach (var selector in selectors)
            {
                var locator = await FindFirstActionableAsync(page, selector);
                if (locator == null)
                {
                    continue;
                }

                var tagName = await GetTagNameAsync(locator);
                if (string.Equals(tagName, "select", StringComparison.OrdinalIgnoreCase))
                {
                    if (!await TrySelectAsync(locator, value))
                    {
                        continue;
                    }
                }
                else
                {
                    await locator.FillAsync(value);
                }

                await locator.PressAsync("Tab");
                await WaitForPostbackAsync(page);

                var actualValue = await ReadControlValueAsync(locator);
                if (IsValueMatched(actualValue, value))
                {
                    return new FillResult
                    {
                        Success = true,
                        Selector = selector,
                        ActualValue = actualValue,
                        ControlType = tagName
                    };
                }
            }

            return new FillResult
            {
                Success = false
            };
        }

        private static async Task<bool> TrySelectAsync(ILocator locator, string value)
        {
            try
            {
                var result = await locator.SelectOptionAsync(new SelectOptionValue { Value = value });
                if (result != null && result.Count > 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var result = await locator.SelectOptionAsync(new SelectOptionValue { Label = value });
                if (result != null && result.Count > 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var options = locator.Locator("option");
                var count = await options.CountAsync();
                for (var i = 0; i < count; i++)
                {
                    var option = options.Nth(i);
                    var text = await option.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(text) &&
                        text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var optionValue = await option.GetAttributeAsync("value");
                        var result = await locator.SelectOptionAsync(new SelectOptionValue
                        {
                            Value = optionValue
                        });
                        return result != null && result.Count > 0;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static async Task<ILocator> FindFirstActionableAsync(IPage page, string selector)
        {
            var locator = page.Locator(selector);
            var count = await locator.CountAsync();
            for (var i = 0; i < count; i++)
            {
                var current = locator.Nth(i);
                if (await current.IsVisibleAsync() && await current.IsEnabledAsync())
                {
                    return current;
                }
            }

            return null;
        }

        private static async Task<string> ReadControlValueAsync(ILocator locator)
        {
            var tagName = await GetTagNameAsync(locator);
            if (string.Equals(tagName, "select", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var selected = locator.Locator("option:checked");
                    if (await selected.CountAsync() > 0)
                    {
                        return (await selected.First.InnerTextAsync()).Trim();
                    }
                }
                catch
                {
                }
            }

            try
            {
                return (await locator.InputValueAsync()).Trim();
            }
            catch
            {
            }

            try
            {
                return (await locator.InnerTextAsync()).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsValueMatched(string actualValue, string expectedValue)
        {
            if (string.IsNullOrWhiteSpace(actualValue))
            {
                return false;
            }

            if (string.Equals(actualValue.Trim(), expectedValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return actualValue.IndexOf(expectedValue, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async Task<string> GetTagNameAsync(ILocator locator)
        {
            var tagName = await locator.EvaluateAsync<string>("element => element.tagName");
            return string.IsNullOrWhiteSpace(tagName) ? string.Empty : tagName.Trim().ToLowerInvariant();
        }

        private static async Task<string> ReadLocatorTextAsync(ILocator locator)
        {
            try
            {
                var text = await locator.InnerTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
            catch
            {
            }

            try
            {
                var value = await locator.GetAttributeAsync("value");
                return value == null ? string.Empty : value.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<bool> HasVisibleLocatorAsync(IPage page, string selector)
        {
            try
            {
                var locator = page.Locator(selector);
                var count = await locator.CountAsync();
                for (var i = 0; i < count; i++)
                {
                    if (await locator.Nth(i).IsVisibleAsync())
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static async Task WaitForPageStableAsync(IPage page)
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

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

        private static async Task WaitForPostbackAsync(IPage page)
        {
            await Task.Delay(150);
            await WaitForPageStableAsync(page);
            await Task.Delay(150);
        }

        private static async Task<string> ReadMessageAsync(IPage page)
        {
            var messageSelectors = new[]
            {
                "span[id$='Label_Message']",
                "label[id$='Label_Message']",
                "#Label_Message"
            };

            foreach (var selector in messageSelectors)
            {
                try
                {
                    var locator = page.Locator(selector);
                    if (await locator.CountAsync() > 0)
                    {
                        var text = await locator.First.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text.Trim();
                        }
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static async Task<string> TryTakeScreenshotAsync(IPage page, string prefix)
        {
            try
            {
                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
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

        private static async Task<string> TryGetTitleAsync(IPage page)
        {
            try
            {
                return await page.TitleAsync();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string[] GetCustomerSelectors()
        {
            return new[]
            {
                "select[id$='dpl_customerid']",
                "select[name$='dpl_customerid']",
                "input[id$='Customer']",
                "input[id$='Cust']",
                "input[id$='CustomerNo']",
                "input[id$='CustNo']",
                "input[name$='Customer']",
                "input[name$='Cust']",
                "input[name$='CustomerNo']",
                "input[name$='CustNo']"
            };
        }

        private static string[] GetItemNoSelectors()
        {
            return new[]
            {
                "input[id$='txt_companyProductId']",
                "input[id$='txt_ProductNumber']",
                "input[id$='txt_productname']",
                "input[id$='ItemNo']",
                "input[id$='PartNo']",
                "input[id$='ProductNo']",
                "input[id$='MaterialNo']",
                "input[name$='ItemNo']",
                "input[name$='PartNo']",
                "input[name$='ProductNo']",
                "input[name$='MaterialNo']"
            };
        }

        private static string[] GetQuantitySelectors()
        {
            return new[]
            {
                "input[id$='txt_OrderNumber']",
                "input[id$='txt_ordersingle']",
                "input[id$='Quantity']",
                "input[id$='Qty']",
                "input[name$='Quantity']",
                "input[name$='Qty']"
            };
        }

        private static string[] GetOrderDateSelectors()
        {
            return new[]
            {
                "input[id$='txt_startDate']",
                "input[id$='OrderDate']",
                "input[name$='OrderDate']"
            };
        }

        private static string[] GetDeliveryDateSelectors()
        {
            return new[]
            {
                "input[id$='txt_merchindiseEndDate']",
                "input[id$='txt_designEndDate']",
                "input[id$='txt_productEndDate']",
                "input[id$='DeliveryDate']",
                "input[name$='DeliveryDate']"
            };
        }

        private static string[] GetRemarkSelectors()
        {
            return new[]
            {
                "textarea[id$='Remark']",
                "input[id$='txt_comment']",
                "textarea[id$='txt_comment']",
                "input[id$='Remark']",
                "textarea[name$='Remark']",
                "input[name$='Remark']"
            };
        }

        private sealed class ClickResult
        {
            public string Selector { get; set; }

            public string Text { get; set; }
        }

        private sealed class FillResult
        {
            public bool Success { get; set; }

            public string Selector { get; set; }

            public string ActualValue { get; set; }

            public string ControlType { get; set; }
        }
    }
}
