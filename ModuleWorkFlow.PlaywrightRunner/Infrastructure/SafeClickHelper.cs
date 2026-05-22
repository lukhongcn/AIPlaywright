using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace ModuleWorkFlow.PlaywrightRunner.Infrastructure
{
    internal static class SafeClickHelper
    {
        private static readonly string[] DangerousKeywords =
        {
            "保存",
            "儲存",
            "存檔",
            "提交",
            "送出",
            "審核",
            "反審核",
            "過帳",
            "刪除",
            "確定",
            "确认",
            "OK",
            "批量更新"
        };

        public static async Task SafeClickAsync(IPage page, ILocator locator, string purpose)
        {
            if (page == null)
            {
                throw new ArgumentNullException("page");
            }

            if (locator == null)
            {
                throw new ArgumentNullException("locator");
            }

            var metadata = await ReadLocatorMetadataAsync(locator);
            var combined = string.Join(" | ", metadata.ToArray());
            if (ContainsDangerousKeyword(combined))
            {
                throw new InvalidOperationException("拒絕點擊危險按鈕，purpose=" + purpose + "，內容=" + combined);
            }

            await locator.ClickAsync();
        }

        private static async Task<List<string>> ReadLocatorMetadataAsync(ILocator locator)
        {
            var metadata = new List<string>();

            var innerText = await TryReadAsync(async () => await locator.InnerTextAsync());
            if (!string.IsNullOrWhiteSpace(innerText))
            {
                metadata.Add(innerText.Trim());
            }

            var inputValue = await TryReadAsync(async () => await locator.InputValueAsync());
            if (!string.IsNullOrWhiteSpace(inputValue))
            {
                metadata.Add(inputValue.Trim());
            }

            var ariaLabel = await TryReadAsync(async () => await locator.GetAttributeAsync("aria-label"));
            if (!string.IsNullOrWhiteSpace(ariaLabel))
            {
                metadata.Add(ariaLabel.Trim());
            }

            var title = await TryReadAsync(async () => await locator.GetAttributeAsync("title"));
            if (!string.IsNullOrWhiteSpace(title))
            {
                metadata.Add(title.Trim());
            }

            var value = await TryReadAsync(async () => await locator.GetAttributeAsync("value"));
            if (!string.IsNullOrWhiteSpace(value))
            {
                metadata.Add(value.Trim());
            }

            return metadata;
        }

        private static bool ContainsDangerousKeyword(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (var keyword in DangerousKeywords)
            {
                if (value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<string> TryReadAsync(Func<Task<string>> action)
        {
            try
            {
                return await action();
            }
            catch
            {
                return null;
            }
        }
    }
}
