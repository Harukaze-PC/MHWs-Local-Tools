# MHWs Local Tools

本工具由玩家独立开发，与 CAPCOM 无隶属或合作关系。《怪物猎人：荒野》相关商标、武器图标及其他游戏素材的权利归 CAPCOM 或其相应权利人所有。

Windows 桌面程序，使用 .NET Framework 4.x。打开「MHWs Local Tools.exe」后，程序会自动读取检测到的本机 Steam 版《怪物猎人：荒野》存档。游戏内保存后，可以点存档栏右侧的「读取存档」刷新。选好角色后，切换「护石工具」或「装饰珠工具」，底部会显示对应的查看和导出按钮。

在「护石工具」中选择评分方式后，点击「查看护石」可按技能或护石名称搜索，选中护石可查看技能、孔位和状态。想在游戏里找到同一枚护石，可以用游戏内的技能筛选缩小范围，再核对技能等级和孔位。点击「导出护石 Excel」可将护石清单另存为 .xlsx 工作簿，每枚护石占一行。

本工具以只读方式访问游戏存档。清单可以另存为独立的 Excel 文件。

## 查看和导出装饰珠

进入「装饰珠工具」可以看到当前角色的图鉴收集率，以及已收集、未持有和图鉴总种数。点击「查看未持有装饰珠」即可查看当前还缺哪些珠子。持有数量包括空闲和已镶嵌的珠子，只有两项都为 0 才算未持有。同一件装备上的镶珠只计一次。

点击「导出装饰珠 Excel」可保存当前角色的完整装饰珠清单。图鉴里的每种珠子各占一行，列出名称、类型、所需孔级、技能与等级、空闲数量、已镶嵌数量、总数量和持有状态。未持有的珠子排在最前，也可以用「持有状态」筛选。装饰珠的查看与导出均可在任意护石评分方式下使用。

## 护石评分

**护石评分 = 技能的等效分 + 孔位的等效分。** 启用评分时，只计算当前方案选中的技能。每级武器技能计 3 分。每级防具技能按提供该技能 Lv1 的单技能装饰珠所需最低孔级计分。每个武器 1 级孔计 3 分，防具孔按孔级计 1、2、3 分。

属性变换没有装饰珠，依照视频示例计 3 分/级。例如攻击 Lv3、属性变换 Lv1、两个防具 1 级孔，在两项技能都选中时合计 9 + 3 + 1 + 1 = **14 分**。[这是一只ZY《护石机制分析&刷取建议》，02:50 起](https://www.bilibili.com/video/BV12gbYzdEnR/?t=170)

本工具补充约定，已核实无珠的属性吸收、属性异常耐性、饭团也按每级 3 分，仅在当前方案选中时计分。「技能等效分赋值说明」中的「技能分值」页按技能列出分值与依据。资料尚未定价时显示「待核对」，得分明细保留已知小计。[MHDB 中文装饰珠数据](https://wilds.mhdb.io/zh-Hans/decorations)

- 「不评分」按护石名称和技能排序，不计算分数。启用评分时，先按分数从高到低排列，同分的护石按名称和技能排序。
- 「按武器/流派推荐」先选武器，再选流派。选「全部武器」时，流派固定为「全部流派」。选定一把武器后，可以切换到该武器的具体流派。每次切换武器都会回到「全部流派」。在所选范围内，每个流派和属性变体分别计分，取最高分。神护石为 17～18 分，超神护石为 19 分及以上。按这是一只ZY公开的护石池，普通鉴定护石的理论上限为 20 分，前提是同一流派计入这枚护石的全部技能。[护石池查询器](https://shadowingszy.com/mhws-charm/)
- 「护石工具」顶部固定显示神护石和超神护石的数量。所有评分方式下，分档统计都沿用在「按武器/流派推荐」中选定的武器、流派和技能。
- 在「按武器/流派推荐」或「自定义选择」中点「编辑技能」。左栏按分类和名称查找可添加的技能，右栏显示已选技能，也可以单独搜索。双击移动一项，或用 Ctrl / Shift 多选后点击添加或移出。底部「本次变更」列出本轮新增和移除的名称，核对后点「应用更改」。点「撤销本次修改」可回到打开窗口时的选择。
- 主窗口按分类显示已应用的技能。推荐模式的分档卡片、护石预览、Excel 分数和「流派参考」页采用所选推荐范围。自定义模式的 Excel 附本次使用的技能清单。

推荐模式下，编辑技能只会调整当前选中的武器与流派。例如，在「弓」的「物理刚射」中追加「火耐性」，只影响这一流派。恢复原有推荐技能时，该技能会回到本来适用的流派或属性变体。点「恢复推荐」可以预览默认配置，再点「应用更改」即可还原所选范围。修改在本次打开程序期间保留，切换武器、流派或评分方式后仍然有效。

点击「技能等效分赋值说明 → 技能分值」可按分类查阅技能的游戏类型、分值和计分依据。当前图鉴收录 179 项技能，按用途分为 14 类。其中 121 项可用于评分，其余 58 项在分值表标为「仅供查阅」。

## 重新编译

在本目录打开 Windows PowerShell，运行下方命令。默认在本目录的上一级创建 `dist\public\MHWs Local Tools.exe`，也可以用 `-OutputPath` 指定输出位置。构建所需的 C# 源码、图标和数据都在本目录，编译时会把运行所需资源嵌入 EXE。

```powershell
& .\scripts\build.ps1
```

### 检查只读构建

在本目录运行下列命令，可检查刚编译的 EXE 是否排除了写档相关实现。测试直接检查程序文件，无需游戏存档。

```powershell
$testDir = Join-Path $env:TEMP ("mhws-readonly-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null
$mainExe = Join-Path $testDir "MHWs Local Tools.exe"
$auditExe = Join-Path $testDir "ReadOnlyAudit.exe"
& .\scripts\build.ps1 -OutputPath $mainExe

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
& $csc /nologo /target:exe /codepage:65001 "/out:$auditExe" .\tests\ReadOnlyAudit.cs
if ($LASTEXITCODE -ne 0) { throw "测试程序编译失败" }
& $auditExe $mainExe
```

### 检查护石清单、推荐范围和 Excel 导出

在本目录运行下列命令。测试用模拟护石检查搜索、排序、详情、四种导出布局，以及下拉框布局和武器、流派切换，不读取游戏存档。

```powershell
$testDir = Join-Path $env:TEMP ("mhws-charm-list-" + [Guid]::NewGuid().ToString("N"))
$sources = @(Get-ChildItem -LiteralPath .\src -File -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$sources += (Resolve-Path .\src\Properties\Edition.cs).Path
foreach ($test in 'CharmListAudit', 'RecommendationScopeAudit', 'SkillPickerAudit') {
    $auditExe = Join-Path $testDir "$test.exe"
    $auditSources = $sources + (Resolve-Path ".\tests\$test.cs").Path
    & .\scripts\Compile.ps1 -SourceFiles $auditSources -AssetsDirectory (Resolve-Path .\assets).Path -OutputPath $auditExe -MainClass $test
    & $auditExe
    if ($LASTEXITCODE -ne 0) { throw "$test 没有通过" }
}
```

## 数据与格式

- 护石评分中的武器类型图标来自[卡普空武器介绍](https://www.monsterhunter.com/wilds/en-us/hunting/weapon/)，图像版权归各权利人。各图标的来源见 [weapon-sources.json](assets/sources/weapon-sources.json)。

- 存档容器和字段编码参考 [RE Save Editor](https://github.com/kvasszn/ree-save-editor/tree/main/src/save)。通过存档所在的 Steam 用户目录确定所属账号。
- 鉴定护石字段解析参考 [Bulk Sell Equipment](https://github.com/zamcoder/bulk-sell-equipment-mhwilds/blob/main/src/reframework/autorun/BulkSell/game.lua)。
- 中文名称与等级来自 [Monster Hunter Wilds Database](https://docs.wilds.mhdb.io/)，编号按[游戏枚举](https://github.com/kvasszn/ree-save-editor/blob/main/assets/mhwilds/enumsmhwilds.json)对应。
- 装饰珠的中文名称、孔级和技能来自[装饰珠数据](https://wilds.mhdb.io/zh-Hans/decorations)。按游戏枚举关联存档编号和数据库编号；无法识别的珠子保留数量，并在名称中显示存档编号。
- Excel 工作簿列出护石的名称、类型、稀有度、固有技能、装饰品孔位、收藏及装备状态。固有技能和装饰品孔位各分三列，未使用的列留空。孔位表示护石自带的孔，不计已镶嵌珠子的技能。遇到「待核对」时，可在程序的「查看护石」中搜索技能或名称，再选中护石查看提示。
