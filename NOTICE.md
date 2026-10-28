# 第三方素材与参考

## 图标

- `鲸鱼娘桌宠助手.ico` / `icon-preview.png` 由 [dsh-pet](https://github.com/PC2005-cloud/dsh-pet)（MIT）npm 包内的 `assets/pic/notify-done.png` 裁切生成。
- 该立绘的角色形象版权归原作者 / DeepSeek 社区所有，本仓库仅作**个人使用的衍生示例**，不主张其版权；如需商用请自行替换为自己的素材（可用 `make-icon.ps1` 指向任意 256×256 透明 PNG）。
- 生成脚本不包含原图，只在运行时读取本机 `dsh-pet` 安装目录里的素材。

## 依赖

- 运行时图标读取、HTTP 调用、WinForms 均来自 .NET Framework，无第三方库。
- 桌宠本身与 DSH 插件属于各自项目，本仓库不包含它们的代码。
