using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PalamedesClothDynamics)), CanEditMultipleObjects]
public sealed class PalamedesClothDynamicsEditor : Editor
{
    bool capeOpen=true,panelsOpen=true,capeAdvanced,capeBindings;
    static GUIContent Label(string title,string tip="")=>new GUIContent(title,tip);
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using(new EditorGUI.DisabledScope(true))EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"),Label("脚本"));
        EditorGUILayout.LabelField("角色布料动力学",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("统一控制披风和裙片。数值越大不一定越柔软，请参考各项提示。编辑态调参后，重新开始动画预览可查看效果。",MessageType.None);
        Field("characterRoot","角色根节点","所有布料共用的角色坐标参考。");
        Field("startupBlendTime","启动渐入时间（秒）","进入游戏或重新启用后，逐渐恢复惯性和拖尾，避免初始姿态差造成甩动。");
        Field("startupSettleSteps","启动静态预热步数","首次动画采样后预先处理接触，清除碰撞修正产生的初始速度；仅重置时执行。");
        capeOpen=EditorGUILayout.BeginFoldoutHeaderGroup(capeOpen,"披风");
        if(capeOpen)
        {
            Field("capeEnabled","启用披风动态");
            using(new EditorGUI.DisabledScope(!serializedObject.FindProperty("capeEnabled").boolValue))
            {
                Field("motionSource","躯干挂点","建议绑定上胸等躯干骨骼，避免抬手带动整片披风。");
                Field("capeRenderer","披风蒙皮");
                Field("followAttachmentHeading","跟随躯干朝向","下摆和碰撞方向跟随实际后背，重力仍保持竖直向下。");
                Field("chainElasticity","回弹跟随","越小越容易产生拖尾；越大越快回到静态形状。");
                Field("motionInertia","位移直跟比例","越小越有移动滞后感；越大越紧跟挂点。");
                Field("drag","阻尼","越大越快消除晃动。");
                Field("gravity","下垂重力");
                Field("restingBackLift","静态后摆角（度）","让下摆略向背后抬起，挂点位置不变。");
                EditorGUILayout.Space();
                Field("enableBodyCollision","启用身体碰撞");
                using(new EditorGUI.DisabledScope(!serializedObject.FindProperty("enableBodyCollision").boolValue))
                {
                    Array(serializedObject.FindProperty("bodyColliders"),"身体碰撞胶囊","胶囊");
                    Field("bodyClearance","碰撞留白（米）");
                    Field("contactFriction","接触阻尼","越大越快消除贴身滑动。");
                }
                capeAdvanced=EditorGUILayout.Foldout(capeAdvanced,"高级形状与稳定性",true);
                if(capeAdvanced)
                {
                    EditorGUI.indentLevel++;
                    Field("shapeFrequency","静态形状拉回强度");Field("bendStiffness","弯曲保持");
                    Field("foldStiffness","横向褶皱保持","越大越能保持每层褶皱宽度。");
                    Field("sectionRotation","褶皱随弯曲旋转");Field("maximumSectionAngle","单层最大旋转（度）");
                    Field("solverIterations","约束迭代次数","增加可改善稳定性，也会增加计算量。");
                    Field("maximumSpeed","最大移动速度（米/秒）");Field("contactSlop","碰撞容差（米）");
                    EditorGUI.indentLevel--;
                }
                capeBindings=EditorGUILayout.Foldout(capeBindings,"骨骼绑定与初始姿态",true);
                if(capeBindings)
                {
                    EditorGUILayout.HelpBox("这些数据由绑定流程生成。更换挂点或骨骼后需重新换算初始姿态，不能只替换引用。",MessageType.Info);
                    Field("columns","横向列数");Field("rows","纵向层数");
                    Array(serializedObject.FindProperty("bones"),"披风骨骼","骨骼");
                    Array(serializedObject.FindProperty("restLocalPositions"),"初始局部位置","位置");
                    Array(serializedObject.FindProperty("restLocalRotations"),"初始局部旋转","旋转");
                    Array(serializedObject.FindProperty("restInRoot"),"角色坐标初始位置","位置");
                    Array(serializedObject.FindProperty("restInAttachment"),"挂点坐标初始位置","位置");
                    Array(serializedObject.FindProperty("restRotationsInAttachment"),"挂点坐标初始旋转","旋转");
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        panelsOpen=EditorGUILayout.BeginFoldoutHeaderGroup(panelsOpen,"裙片与附属布料");
        if(panelsOpen)
        {
            var panels=serializedObject.FindProperty("panels");
            if(!serializedObject.isEditingMultipleObjects)
                panels.arraySize=Mathf.Max(0,EditorGUILayout.IntField("布片数量",panels.arraySize));
            for(int i=0;i<panels.arraySize;i++)DrawPanel(panels.GetArrayElementAtIndex(i),i);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        bool changed=serializedObject.ApplyModifiedProperties();
        if(changed&&Application.isPlaying)foreach(var item in targets)((PalamedesClothDynamics)item).ResetSimulation();
        using(new EditorGUI.DisabledScope(!Application.isPlaying))
            if(GUILayout.Button("重置运行时模拟"))foreach(var item in targets)((PalamedesClothDynamics)item).ResetSimulation();
        if(targets.Length==1)
        {
            var cloth=(PalamedesClothDynamics)target;
            if(cloth.capeEnabled&&(!cloth.characterRoot||!cloth.motionSource||!cloth.capeRenderer))
                EditorGUILayout.HelpBox("披风缺少角色根节点、躯干挂点或蒙皮引用。",MessageType.Warning);
        }
    }
    void DrawPanel(SerializedProperty p,int index)
    {
        var name=p.FindPropertyRelative("partName").stringValue;
        string title=name=="FrontPanel"?"腰前布片":name=="SidePanel"?"腰侧布片":name=="Hair"?"头发":name=="Skirt"?"裙装":"布片 "+(index+1);
        p.isExpanded=EditorGUILayout.Foldout(p.isExpanded,title,true);
        if(!p.isExpanded)return;
        EditorGUI.indentLevel++;
        Field(p,"enabled","启用动态");
        using(new EditorGUI.DisabledScope(!p.FindPropertyRelative("enabled").boolValue))
        {
            Field(p,"motionSource","运动挂点");Field(p,"collisionRenderer","布片蒙皮");
            Field(p,"maximumBend","最大摆幅（度）");Field(p,"movementResponse","平移惯性响应");
            Field(p,"rotationResponse","转向惯性响应");Field(p,"dampingTime","回落缓冲（秒）","越大越柔缓，回落时间越长。");
            Field(p,"followAttachmentRotation","摆动平面跟随挂点");
            Field(p,"enableLegCollision","启用腿部碰撞");
            using(new EditorGUI.DisabledScope(!p.FindPropertyRelative("enableLegCollision").boolValue))
            {
                Array(p.FindPropertyRelative("legColliders"),"腿部碰撞胶囊","胶囊");
                Field(p,"collisionClearance","碰撞留白（米）");Field(p,"contactActivationDepth","接触启动深度（米）","忽略轻微重叠，减少细小抖动。");
                Field(p,"maximumContactBend","布片最大避让角（度）");Field(p,"maximumAnchorContactBend","腰口最大避让角（度）");
                Field(p,"contactReturnSpeed","接触回落速度（度/秒）");
            }
            var binding=p.FindPropertyRelative("bones");
            binding.isExpanded=EditorGUILayout.Foldout(binding.isExpanded,"绑定与初始姿态",true);
            if(binding.isExpanded)
            {
                Field(p,"partName","内部标识");Field(p,"characterRoot","角色参考节点");Field(p,"localSwingAxis","局部摆动轴");
                Array(binding,"布片骨骼","骨骼",false);Array(p.FindPropertyRelative("restLocalRotations"),"初始局部旋转","旋转");
            }
        }
        EditorGUI.indentLevel--;
    }
    void Field(string name,string title,string tip="")=>EditorGUILayout.PropertyField(serializedObject.FindProperty(name),Label(title,tip),true);
    void Field(SerializedProperty parent,string name,string title,string tip="")=>EditorGUILayout.PropertyField(parent.FindPropertyRelative(name),Label(title,tip),true);
    static void Array(SerializedProperty p,string title,string item,bool fold=true)
    {
        if(fold)p.isExpanded=EditorGUILayout.Foldout(p.isExpanded,title,true);
        else EditorGUILayout.LabelField(title);
        if(fold&&!p.isExpanded)return;
        EditorGUI.indentLevel++;
        p.arraySize=Mathf.Max(0,EditorGUILayout.IntField("数量",p.arraySize));
        for(int i=0;i<p.arraySize;i++)EditorGUILayout.PropertyField(p.GetArrayElementAtIndex(i),Label(item+" "+(i+1)),true);
        EditorGUI.indentLevel--;
    }
}
