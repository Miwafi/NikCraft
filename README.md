# NikCraft

用 **C# + OpenTK + OpenGL 3.3** 从零实现的类 Minecraft 体素沙盒游戏。纯代码生成，除 OpenTK 与 System.Drawing 外无任何第三方依赖，材质可自由替换。

![platform](https://img.shields.io/badge/platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![OpenGL](https://img.shields.io/badge/OpenGL-3.3%20Core-5586A4)

---

## 特性

**世界生成**
- 无限程序化地形：大陆噪声叠加丘陵与细节层，海岸线自动平缓
- 四种生物群系：平原 / 森林 / 沙漠 / 雪原，按温度与湿度噪声划分
- 洞穴系统：3D 噪声的"溶洞"与"隧道"两种形态
- 矿脉分布：煤 / 铁 / 金 / 钻石，按深度分层
- 跨区块边界的树木，树冠自动修剪圆角

**渲染**
- 区块网格构建时剔除被遮挡的面，并烘焙**环境光遮蔽（AO）**，四角独立明暗
- 四边形对角线按 AO 差异自动翻转，消除接缝
- 视锥剔除 + 距离雾，远处地形自然融入天空
- 水 / 玻璃 / 冰走独立的混合渲染通道，由远及近排序
- 树叶使用 alpha 裁剪（cutout），保留深度写入避免排序问题
- 昼夜循环：太阳方位、天空渐变、黄昏色调与环境光强度随时间变化

**玩法**
- 玩家物理：重力、AABB 碰撞、疾跑、潜行、游泳、创造飞行
- **动态视角摇晃**：行走时头部起伏、左右摆动与轻微倾斜
- 方块破坏 / 放置 / 中键取方块，附**破坏粒子效果**
- 9 格快捷栏 + 物品名称提示、准星、`F3` 调试面板

**工程**
- 区块生成与网格构建在后台线程池完成，主线程仅负责 GPU 上传，并有每帧预算限制
- 脏区块重建合并入队，玩家编辑与后台构建用版本号避免竞态
- 程序化 5×7 像素字体，HUD 无需任何字体资源文件

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

## 命令行参数

用于自动化验证与演示：

| 参数 | 说明 |
|---|---|
| `--shot <帧数>` | 运行指定帧数后自动截图并退出 |
| `--walk` | 自动持续前进 |
| `--dig` | 自动持续破坏准星所指方块 |

例如跑 250 帧并截图：

```powershell
.\bin\Release\net8.0-windows\NikCraft.exe --shot 250 --walk --dig
```

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

## 实现要点

几个容易踩坑、已专门处理的地方：

**矩阵约定** — OpenTK 的 `Matrix4` 是行主序存储，且其访问器语义等同于 GLSL 的列向量约定。因此上传 uniform 时 **`transpose` 必须为 `false`**，组合顺序直接写 `view * projection` 即可。

**uniform 写入** — 全部使用 `GL.ProgramUniform*` 并显式传入程序句柄，不依赖"当前绑定的 program"，避免多着色器切换时的隐性错写。

**纹理 Y 轴** — 图集在内存中的第 0 行是图像顶部，而 OpenGL 纹理第 0 行是 `v = 0`（底部）。写入图集时按 `AtlasSize - 1 - row` 翻转，UV 计算才能与采样一致。

**GL 对象与线程** — 区块在后台线程构造，因此 `GpuMesh` 的 VAO/VBO 一律延迟到主线程首次 `Upload` 时才创建；无 GL 上下文的线程不触碰任何 GL 函数。

**碰撞求解** — 玩家按 X/Y/Z 三轴分别以 0.05 格为步长推进，可自然处理贴墙滑行与落地；粒子则用分轴回退并对落地做速度衰减。

---

## 日志与排查

运行时日志写入 exe 同级的 `nikcraft.log`；致命异常会写入 `crash.log` 并弹出提示框。

---

## 授权

本项目为学习与演示用途。Minecraft 是 Mojang Studios 的商标，本项目与之无关联。
