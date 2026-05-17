# 日随伴侣发布流程

> 最后更新：2026-05-17
> 当前版本：`1.0.1.0`
> 当前仓库：`https://github.com/anmili2022/RouletteRecorder.Dalamud`
> 当前 Release：`https://github.com/anmili2022/RouletteRecorder.Dalamud/releases/tag/v1.0.1.0`

本文档用于下次快速发布，尽量按顺序复制执行。

## 1. 发布前确认

确认当前在仓库根目录：

```powershell
pwd
```

应为：

```text
E:\git\RouletteRecorder.Dalamud
```

确认当前分支：

```powershell
git branch --show-current
```

应为：

```text
master
```

确认工作区状态：

```powershell
git status --short
```

如果有未提交修改，先确认这些修改都应该进入本次发布。

确认 GitHub CLI 登录：

```powershell
gh auth status
```

## 2. 选择新版本号

当前版本是：

```text
1.0.1.0
```

下次普通功能更新建议使用：

```text
1.0.2.0
```

以下命令里的版本号按实际发布版本替换：

```powershell
$oldVersion = "1.0.1.0"
$newVersion = "1.0.2.0"
```

## 3. 修改版本号

版本号主要在：

```text
RouletteRecorder.Dalamud/RouletteRecorder.Dalamud.csproj
```

可手动修改这四项：

```xml
<Version>1.0.2.0</Version>
<AssemblyVersion>1.0.2.0</AssemblyVersion>
<FileVersion>1.0.2.0</FileVersion>
<InformationalVersion>1.0.2.0</InformationalVersion>
```

也可以用命令替换：

```powershell
$oldVersion = "1.0.1.0"
$newVersion = "1.0.2.0"
(Get-Content -Encoding UTF8 RouletteRecorder.Dalamud\RouletteRecorder.Dalamud.csproj) -replace [regex]::Escape($oldVersion), $newVersion | Set-Content -Encoding UTF8 RouletteRecorder.Dalamud\RouletteRecorder.Dalamud.csproj
(Get-Content -Encoding UTF8 docs\HANDOFF.md) -replace [regex]::Escape($oldVersion), $newVersion | Set-Content -Encoding UTF8 docs\HANDOFF.md
(Get-Content -Encoding UTF8 docs\RELEASE.md) -replace [regex]::Escape($oldVersion), $newVersion | Set-Content -Encoding UTF8 docs\RELEASE.md
```

确认旧版本号是否还有残留：

```powershell
rg "1\.0\.1\.0|v1\.0\.1\.0"
```

如果是发布历史链接中的旧版本，可以保留；如果是当前版本字段，应改成新版本。

## 4. 构建 Release

建议先删除旧输出，避免旧包混入。

先确认路径：

```powershell
Resolve-Path -LiteralPath output
```

如果路径确实是仓库下的 `output`，再删除：

```powershell
Remove-Item -LiteralPath "E:\git\RouletteRecorder.Dalamud\output" -Recurse -Force
```

构建：

```powershell
dotnet build -c Release
```

必须看到：

```text
已成功生成。
0 个警告
0 个错误
```

## 5. 检查输出 manifest

查看输出清单：

```powershell
Get-Content -Encoding UTF8 output\RouletteRecorder.Dalamud.json
```

确认这些字段正确：

```json
"Name": "日随伴侣"
"InternalName": "日随伴侣卫月版"
"AssemblyVersion": "1.0.2.0"
"DalamudApiLevel": 15
```

检查发布包内容：

```powershell
tar -tf output\RouletteRecorder.Dalamud\latest.zip
```

正常应类似：

```text
CsvHelper.dll
RouletteRecorder.Dalamud.deps.json
RouletteRecorder.Dalamud.dll
RouletteRecorder.Dalamud.json
```

不要出现嵌套的旧包：

```text
latest.zip
```

检查 zip 内 manifest：

```powershell
tar -xOf output\RouletteRecorder.Dalamud\latest.zip RouletteRecorder.Dalamud.json
```

也要确认：

```json
"InternalName": "日随伴侣卫月版"
"AssemblyVersion": "1.0.2.0"
```

## 6. 更新 repo.json

`repo.json` 是用户插件仓库读取的清单，需要更新版本号、时间戳和下载链接。

可以用下面命令自动更新：

```powershell
$newVersion = "1.0.2.0"
$repo = Get-Content -Raw -Encoding UTF8 repo.json | ConvertFrom-Json
$link = "https://github.com/anmili2022/RouletteRecorder.Dalamud/releases/download/v$newVersion/latest.zip"
$repo[0].AssemblyVersion = $newVersion
$repo[0].LastUpdate = "$([int][double](Get-Date -UFormat %s))"
$repo[0].DownloadLinkInstall = $link
$repo[0].DownloadLinkTesting = $link
$repo[0].DownloadLinkUpdate = $link
$repo | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 repo.json
```

检查：

```powershell
Get-Content -Encoding UTF8 repo.json
```

## 7. 写 Release Notes

发布说明写到：

```text
output/release_notes.md
```

示例命令：

```powershell
Set-Content -Encoding UTF8 output\release_notes.md "# 日随伴侣 v1.0.2.0`n`n## 主要更新`n`n- 写这里。`n- 写这里。`n`n## 构建产物`n`n- latest.zip：Dalamud 插件发布包。"
```

如果内容较多，也可以直接用编辑器打开：

```powershell
notepad output\release_notes.md
```

## 8. 提交、打标签、推送

查看变更：

```powershell
git status --short
git diff --stat
```

提交：

```powershell
git add .
git commit -m "chore: release v1.0.2.0"
```

打标签：

```powershell
git tag -a v1.0.2.0 -m "日随伴侣 v1.0.2.0"
```

推送：

```powershell
git push origin master
git push origin v1.0.2.0
```

## 9. 创建 GitHub Release

推荐直接上传本地构建好的发布包：

```powershell
gh release create v1.0.2.0 output\RouletteRecorder.Dalamud\latest.zip --title "日随伴侣 v1.0.2.0" --notes-file output\release_notes.md
```

如果 Release 已经存在，需要覆盖上传包：

```powershell
gh release upload --clobber v1.0.2.0 output\RouletteRecorder.Dalamud\latest.zip
```

查看发布结果：

```powershell
gh release view v1.0.2.0 --json tagName,name,url,assets,publishedAt
```

应能看到资产：

```text
latest.zip
```

## 10. 最终验证

查看 Release 页面：

```text
https://github.com/anmili2022/RouletteRecorder.Dalamud/releases
```

确认下载链接：

```text
https://github.com/anmili2022/RouletteRecorder.Dalamud/releases/download/v1.0.2.0/latest.zip
```

确认本地干净：

```powershell
git status --short
```

无输出即干净。

确认最新提交：

```powershell
git log -1 --oneline
```

确认标签指向当前提交：

```powershell
git tag --points-at HEAD
```

应包含：

```text
v1.0.2.0
```

## 11. GitHub Actions 说明

当前工作流文件：

```text
.github/workflows/build.yml
```

当前发布辅助脚本：

```text
.github/scripts/Make-Repo.ps1
```

理论上发布 GitHub Release 时，Actions 会：

1. 构建 Release。
2. 上传 `Release/RouletteRecorder.Dalamud/latest.zip` 到 Release。
3. 运行 `Make-Repo.ps1` 生成 `repo.json`。
4. 提交并推送更新后的 `repo.json`。

但为了避免 Actions 环境或权限异常导致发布延迟，当前推荐以上“本地构建 + 手动创建 Release + 手动更新 repo.json”的可靠流程。

## 12. 常见坑

1. 不要上传 `output/latest.zip`。
2. 必须上传 `output/RouletteRecorder.Dalamud/latest.zip`。
3. zip 内不要包含旧的 `latest.zip`。
4. zip 内 manifest 的 `InternalName` 必须是 `日随伴侣卫月版`。
5. `LocalizeOutputManifest.ps1` 不要硬编码版本号。
6. `repo.json` 的三个下载链接都要指向新 tag。
7. 打 tag 前先 commit，否则 tag 可能不包含本次改动。
8. 如果 Release 创建失败，先检查 tag 是否已推送。
9. 如果要重发同版本，使用 `gh release upload --clobber`。
10. 发布后不要忘记检查插件仓库地址：

```text
https://raw.githubusercontent.com/anmili2022/RouletteRecorder.Dalamud/refs/heads/master/repo.json
```
