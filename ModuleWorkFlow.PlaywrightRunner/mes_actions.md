# MES Playwright Runner Actions

## General Rules

This file describes the actions that can be executed by `ModuleWorkFlow.PlaywrightRunner`.

Current scope: login test only.

The AI must not generate Playwright code.
The AI must not invent new actions.
The AI must only select an existing action and fill params.
If required params are missing, the AI should return `need_more_info`.
If the user requests create, update, delete, save, submit, approve, post, or other write operations, return `unsupported`.

Output must be JSON only.

---

## Output Format

```json
{
  "status": "ready | need_more_info | unsupported",
  "action": "login_test",
  "params": {},
  "missingParams": [],
  "question": "",
  "confidence": 0.95
}
```

---

## action: login_test

### Purpose

Open the MES login page, fill username and password, click the login button, and return the login result.

### URL

```text
http://localhost:5008/Login.aspx
```

### Handler

```text
login_test
```

### Required Params

| Param | Description | Example |
|---|---|---|
| loginUrl | MES login page URL | http://localhost:5008/Login.aspx |
| userName | MES employee/user ID | testuser |
| password | MES password | testpassword |

### Optional Params

None for now.

### Example User Inputs

- 測試登入
- 登入 MES
- 幫我登入
- 打開登入頁
- 測一下 Login.aspx

### If Missing Params

If missing `loginUrl`, ask:

```text
請提供登入頁地址。
```

If missing `userName`, ask:

```text
請提供員工編號或登入帳號。
```

If missing `password`, ask:

```text
請提供密碼。
```

### Example Ready Output

```json
{
  "status": "ready",
  "action": "login_test",
  "params": {
    "loginUrl": "http://localhost:5008/Login.aspx",
    "userName": "testuser",
    "password": "testpassword"
  },
  "missingParams": [],
  "question": "",
  "confidence": 0.95
}
```

### Example Need More Info Output

```json
{
  "status": "need_more_info",
  "action": "login_test",
  "params": {
    "loginUrl": "http://localhost:5008/Login.aspx"
  },
  "missingParams": ["userName", "password"],
  "question": "請提供員工編號和密碼。",
  "confidence": 0.95
}
```

### Example Unsupported Output

```json
{
  "status": "unsupported",
  "action": null,
  "params": {},
  "missingParams": [],
  "question": "目前只支援登入測試，不支援其他操作。",
  "confidence": 1.0
}
```
