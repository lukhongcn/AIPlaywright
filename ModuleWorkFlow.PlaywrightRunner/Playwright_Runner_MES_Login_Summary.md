# Playwright Runner + MES 登入測試進度 Summary

## 1. 目標方向

目前目標是：

```text
用 Playwright 操作現有網頁版 MES
先只做登入測試 / 查詢
後續接千問 API
讓自然語言轉成 action + params
再由 Runner 執行 Playwright
```

核心原則：

```text
不改老 MES 框架
不引用 BLL / DAL / Model / Utility
不讓 AI 直接生成 Playwright 代碼
AI 只輸出 JSON
Runner 根據 action 白名單執行固定流程
```

---

## 2. 技術環境

目前環境：

```text
.NET Framework 4.7.2
VS2022
C# 7.3
Browser: Microsoft Edge
Playwright: 1.59.0
Namespace:
- ModuleWorkFlow.PlaywrightTests
- ModuleWorkFlow.PlaywrightRunner
```

注意：

```text
.NET Framework 4.7.2 / C# 7.3 不支持 await using
所以 Playwright browser/playwright 釋放要用 try/finally
```

---

## 3. PlaywrightTests 測試專案

建立過：

```text
ModuleWorkFlow.PlaywrightTests
```

用途：

```text
給開發者自己測試 Playwright selector 和登入流程
不給普通同事使用
```

### 遇到過的問題和解法

#### 3.1 Playwright 版本衝突

錯誤：

```text
Detected package downgrade: Microsoft.Playwright from 1.59.0 to 1.58.0
```

原因：

```text
Microsoft.Playwright.NUnit 1.59.0 需要 Microsoft.Playwright >= 1.59.0
但項目中直接引用了 Microsoft.Playwright 1.58.0
```

解法：

```text
Microsoft.Playwright        1.59.0
Microsoft.Playwright.NUnit  1.59.0
```

#### 3.2 Assert ambiguous

錯誤：

```text
Assert is an ambiguous reference between NUnit.Framework.Assert and Microsoft.VisualStudio.TestTools.UnitTesting.Assert
```

解法：

```csharp
刪掉：
using Microsoft.VisualStudio.TestTools.UnitTesting;

保留：
using NUnit.Framework;
```

#### 3.3 NUnit3TestAdapter 問題

錯誤：

```text
NUnit.VisualStudio does not exist...
SelfRegisteredExtensions.cs
```

解法：

```text
NUnit3TestAdapter 降到 5.0.0
```

#### 3.4 Main 方法問題

錯誤：

```text
Program does not contain a static Main method
```

原因：

```text
測試專案被設成 Console App
```

解法：

```text
Output Type 改成 Class Library
```

#### 3.5 dotnet test 對老 .NET Framework 不友好

錯誤：

```text
dotnet.exe 嘗試直接執行 net472 dll
The specified executable is not a valid application for this OS platform
```

結論：

```text
.NET Framework 4.7.2 老項目不要優先用 dotnet test
測試專案可用 VS Test Explorer 或 vstest.console.exe
```

---

## 4. PlaywrightRunner Console 項目

新建：

```text
ModuleWorkFlow.PlaywrightRunner
```

項目類型：

```text
Console App (.NET Framework)
Target Framework: .NET Framework 4.7.2
```

安裝：

```text
Microsoft.Playwright 1.59.0
```

不需要安裝：

```text
Microsoft.Playwright.NUnit
NUnit
NUnit3TestAdapter
```

Runner 第一版已經跑通：

```text
request.json
  ↓
ModuleWorkFlow.PlaywrightRunner.exe
  ↓
Playwright + Edge
  ↓
打開 Login.aspx
  ↓
填帳密
  ↓
點登入
  ↓
輸出 JSON 結果
```

---

## 5. Edge 設定

因為使用 Edge，所以 Playwright 啟動方式是：

```csharp
browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Channel = "msedge",
    Headless = false,
    SlowMo = 300
});
```

重點：

```text
BrowserType 是 Chromium
Channel 是 msedge
```

這樣會使用本機已安裝的 Microsoft Edge。

---

## 6. C# 7.3 兼容寫法

不能用：

```csharp
await using
```

應使用：

```csharp
IPlaywright playwright = null;
IBrowser browser = null;

try
{
    playwright = await Playwright.CreateAsync();

    browser = await playwright.Chromium.LaunchAsync(...);

    // do work
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
```

這是目前 Runner 裡應該遵守的風格。

---

## 7. request.json 方式已跑通

目前 Runner 支持：

```bat
ModuleWorkFlow.PlaywrightRunner.exe request.json
```

推薦目錄結構：

```text
ModuleWorkFlow.PlaywrightRunner  run.bat
  request.json

  bin    Debug      ModuleWorkFlow.PlaywrightRunner.exe
      Microsoft.Playwright.dll
      ...
```

`run.bat`：

```bat
@echo off
cd /d %~dp0

.in\Debug\ModuleWorkFlow.PlaywrightRunner.exe request.json

pause
```

`request.json` 放在 **run.bat 同目錄**，不是 exe 同目錄。

---

## 8. 目前 request.json

目前內容：

```json
{
  "action": "login_test",
  "params": {
    "loginUrl": "http://localhost:5008/Login.aspx",
    "userName": "testuser",
    "password": "testpassword"
  }
}
```

需要把：

```json
"userName": "testuser",
"password": "testpassword"
```

改成實際測試帳號密碼。

---

## 9. 給千問看的 action 說明文件

文件名建議：

```text
mes_actions.md
```

用途：

```text
給千問 API prompt 用
告訴千問目前有哪些 action
每個 action 需要哪些 params
缺少 params 時怎麼問
禁止千問發明新 action
禁止千問生成 Playwright 代碼
```

目前只包含：

```text
login_test
```

用途：

```text
打開 MES 登入頁
填入 userName/password
點擊登入
返回登入結果
```

---

## 10. 給程序做白名單的 action JSON

文件名建議：

```text
mes_actions.json
```

目前內容大概是：

```json
[
  {
    "action": "login_test",
    "readOnly": true,
    "url": "http://localhost:5008/Login.aspx",
    "requiredParams": ["loginUrl", "userName", "password"],
    "optionalParams": [],
    "handler": "login_test",
    "description": "Open the MES login page, fill username and password, click login, and return the login result."
  }
]
```

用途：

```text
給 C# 程序做 action 白名單校驗
防止 AI 返回未允許的 action
防止缺少 requiredParams 仍然執行
```

---

## 11. 文件分工

建議分工：

```text
AGENTS.md
  給 Codex 編程用
  規定不要亂改老框架、不要重構、不要引用 BLL/DAL

mes_actions.md
  給千問 API 用
  告訴千問有哪些 action、需要哪些參數、缺參數怎麼問

mes_actions.json
  給 C# 程序用
  做 action 白名單和 requiredParams 校驗

request.json
  給 PlaywrightRunner.exe 用
  真正執行某一次 action
```

不要把這幾個混在一起。

---

## 12. 今日最重要成果

已跑通：

```text
.NET Framework 4.7.2 Console Runner
+ Playwright
+ Edge
+ Login.aspx
+ request.json
```

這是後續接千問 API 的基礎。

---

## 13. 下一步建議

下次繼續時，從這裡開始：

### 13.1 接千問 API 前，先做本地 Prompt 測試

讓千問根據：

```text
mes_actions.md
用戶自然語言
```

輸出：

```json
{
  "status": "ready",
  "action": "login_test",
  "params": {
    "loginUrl": "http://localhost:5008/Login.aspx"
  },
  "missingParams": [],
  "question": "",
  "confidence": 0.95
}
```

注意：

```text
不要把真實密碼發給千問 API
```

更安全做法：

```text
千問只判斷 action 和 loginUrl
userName/password 由本地 request.json / 環境變數 / 手動輸入補充
```

### 13.2 做一個 QwenClient

可以新建：

```text
ModuleWorkFlow.AiQueryConsole
```

或者先在 Runner 旁邊做簡單測試。

流程：

```text
用戶輸入自然語言
  ↓
讀 mes_actions.md
  ↓
調千問 API
  ↓
得到 action JSON
  ↓
補充本地帳密
  ↓
生成 request.json
  ↓
啟動 PlaywrightRunner.exe
  ↓
讀取 Runner JSON 輸出
```

### 13.3 後續增加 query_order

等 login_test 完整跑穩後，再加：

```text
query_order
```

步驟：

```text
1. 用 Playwright 找訂單查詢頁 selector
2. 寫 query_order handler
3. 更新 mes_actions.md
4. 更新 mes_actions.json
5. 測 request.json
6. 再讓千問識別自然語言
```

---

## 14. 給 Codex 的下一個任務提示

可以直接給 Codex：

```text
請在 ModuleWorkFlow.PlaywrightRunner 中做最小改動：

1. 保持 .NET Framework 4.7.2 / C# 7.3 兼容，不要使用 await using。
2. 保持不引用老 MES 項目。
3. 目前只支持 action = login_test。
4. 從 request.json 讀取 action 和 params。
5. 根據 mes_actions.json 校驗 action 是否允許、requiredParams 是否完整。
6. userName/password 優先從 request.json 讀取；如果沒有，從 MES_USER / MES_PASSWORD 環境變數讀取。
7. Console 最後只輸出 JSON。
8. 不要新增複雜框架，不要重構。
```

---

## 15. 下次對話接續提示

下次可以直接說：

```text
繼續接千問 API。我目前已經跑通 ModuleWorkFlow.PlaywrightRunner：
- .NET Framework 4.7.2
- Playwright 1.59.0
- Edge channel msedge
- request.json 調用 login_test
- run.bat 可以啟動 Runner
請從 mes_actions.md + QwenClient 開始帶我做。
```
