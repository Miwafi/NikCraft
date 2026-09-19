# NikCraft

![platform](https://img.shields.io/badge/platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![OpenGL](https://img.shields.io/badge/OpenGL-3.3%20Core-5586A4)

---

## 特性

**世界生成**
- 四种生物群系：平原 / 森林 / 沙漠 / 雪原，按温度与湿度噪声划分
- 洞穴系统：3D 噪声的"溶洞"与"隧道"两种形态
- 矿脉分布：煤 / 铁 / 金 / 钻石，按深度分层

---

## 环境要求

| 项目 | 要求 |
|---|---|
| 操作系统 | Windows（材质加载依赖 `System.Drawing`） |
| SDK | .NET 8 SDK |
| 显卡 | 支持 OpenGL 3.3 Core Profile |

---

## 构建与运行

```powershell
# 编译
dotnet build -c Release

# 运行
dotnet run -c Release
```

产物路径：`bin\Release\net8.0-windows\NikCraft.exe`

首次运行时会在 exe 同级目录创建 `materials\`、`screenshots\`，并输出 `nikcraft.log`。

---

## 操作说明

| 按键 | 功能 |
|---|---|
| `W` `A` `S` `D` | 移动 |
| `Space` | 跳跃 / 水中上浮 / 飞行上升 |
| `Ctrl` | 疾跑（飞行时加速） |
| `Shift` | 潜行（飞行时下降） |
| `F` | 切换飞行模式 |
| `B` | 切换视角摇晃 |
| `G` | 切换线框模式 |
| `T` | 暂停 / 恢复时间流动 |
| `鼠标左键` | 破坏方块 |
| `鼠标右键` | 放置方块 |
| `鼠标中键` | 拾取准星所指方块 |
| `1` – `9` / `滚轮` | 选择快捷栏 |
| `+` / `-` | 增减视距 |
| `F2` | 截图（保存到 `screenshots\`） |
| `F3` | 调试面板 |
| `F11` | 全屏切换 |
| `Esc` | 释放 / 捕获鼠标 |

---

## 材质定制

所有方块材质都是**外置 PNG**，位于 exe 同级的 `materials\` 目录：

```
materials\
├── grass_top.png       grass_side.png      dirt.png
├── stone.png           cobblestone.png     mossy_cobblestone.png
├── sand.png            sandstone.png       gravel.png
├── snow.png            water.png           ice.png
├── oak_log_side.png    oak_log_top.png     oak_leaves.png
├── birch_log_side.png  birch_log_top.png   birch_leaves.png
├── planks.png          glass.png           bricks.png
├── coal_ore.png        iron_ore.png        gold_ore.png
├── diamond_ore.png     bedrock.png         cactus_side.png
├── cactus_top.png      pumpkin_side.png    pumpkin_top.png
```

规则：

- 每张图对应图集中的一个 tile，**推荐 16×16**，其它尺寸会以最近邻缩放到 16×16（像素画硬边不糊）
- **首次运行**会把 30 张内置材质导出为 PNG，可直接用任意图像编辑器修改
- **删除某个 PNG** 即回退到该材质的内置生成版本，并在下次启动时重新导出
- 透明通道有效：`glass.png` 中间留空即得透明玻璃，`oak_leaves.png` 挖孔即得镂空树叶
- 修改后重新启动游戏即可生效

---

## 项目结构

```
NikCraft.csproj          项目文件（net8.0-windows / OpenTK / System.Drawing.Common）
Program.cs               入口，解析命令行参数并创建窗口
Game.cs                  主循环：输入、更新、渲染、HUD 组装

Core/                    平台与图形底层封装
  Shader.cs              GLSL 程序封装，uniform 使用 ProgramUniform 系列
  Texture2D.cs           RGBA8 纹理上传
  GpuMesh.cs             VAO/VBO/EBO 封装（GL 对象惰性创建）
  Frustum.cs             视锥体平面提取与 AABB 剔除
  InputState.cs          逐帧键鼠状态（含按下沿与鼠标增量钳制）
  Screenshot.cs          帧缓冲读回与 BMP 写出
  Log.cs                 文件日志

Voxel/                   体素世界
  Blocks.cs              方块注册表与纹理图集索引
  Chunk.cs               16×128×16 区块数据 + GPU 网格
  ChunkMesher.cs         面剔除 + 环境光遮蔽的网格构建
  World.cs               区块加载/卸载、后台线程调度、方块读写
  WorldGenerator.cs      地形、生物群系、洞穴、矿脉、树木
  Noise.cs               Perlin 噪声与 fBm
  Raycast.cs             Amanatides & Woo 体素射线步进

Render/                  渲染与界面
  BlockAtlas.cs          图集拼装（外置 PNG 优先，内置兜底）
  MaterialLibrary.cs     PNG 读写与缩放
  Shaders.cs             全部 GLSL 源码
  WorldRenderer.cs       区块不透明/透明通道渲染
  SkyRenderer.cs         全屏三角形天空与太阳
  SelectionRenderer.cs   选中方块线框
  BlockParticles.cs      破坏粒子系统
  UIRenderer.cs          批处理 2D 渲染
  BitmapFont.cs          程序化 5×7 像素字体
  Hud.cs                 准星、快捷栏、调试面板

Gameplay/
  Player.cs              玩家物理、碰撞、视角摇晃
```

---
