PokerCharacter / 角色资产交付

入口：PokerCharacter.prefab，根变换为位置 0、旋转 0、缩放 1。
Models：Humanoid FBX、Visual 子预制体、LastChip 网格。
Materials / Textures：角色与筹码材质、基础色/法线/金属度/粗糙度贴图。
Animations：Idle、Listen、LastChip、Tarot 动画及 Animator Controller。

运行脚本：Assets/Scripts/Presentation/Characters/Runtime/PokerCharacterIdle.cs。
通过 NotifyChipsHeard、PlayLastChipBeat、PlaySecretBeat 可触发待机变化。
眨眼与筹码跟随在 LateUpdate 计算；模型重导入后处理保证表情法线/切线变化局限于脸部邻域。
模型后处理：Assets/Scripts/Presentation/Characters/Editor/PokerCharacterModelPostprocessor.cs。

Shader：Assets/GameAssets/Rendering/Shaders/CharacterDeferred.shader 与 Input/Passes HLSL。
Renderer：Assets/Settings/CharacterRenderer.asset，保留原 Charactor GUID；在 MainURP Renderer 列表索引 3。
层：Characters（8）、GothicChapel（9）、bgmesh（10）；相机遮罩与描边排除层依赖这些层号。

测试场景：
Assets/Scenes/CharacterValidation.unity：角色、教堂、灯光和反射探针组合验证。
Assets/Scenes/CharacterValidation/：LightingData、两组光照贴图和烘焙反射探针，随场景提交。
Assets/HandSkinValidation/HandSkinHumanoidTest.unity：Humanoid 手指与手部蒙皮验证。
Assets/HandSkinValidation/：手部测试动画、材质、Shader、预制体、运行探针及编辑器工具。
Lighting Settings 已集中到 Assets/Settings/Lighting，场景保持原 GUID 绑定。

Tools > Hand Skin Validation 下提供检查入口：
7 Validate Character Shader：角色渲染/几何描边与屏幕描边排除层验证。
9 Validate Standard Character Prefab：Avatar、归一化变换与预制体引用验证。
12 Validate Poker Character Idle：Humanoid 动画采样与脚步漂移验证。
14 Reimport and Validate Blink Normals：模型重导入、眨眼法线/切线及非眼部顶点稳定性验证。
1..6、8、10、11 是资产生成/重新配置操作；已提交资产直接使用无需重建。
工具运行产生的截图、诊断输出位于 Art/HandSkinReview/Unity，按仓库策略保留本地。
