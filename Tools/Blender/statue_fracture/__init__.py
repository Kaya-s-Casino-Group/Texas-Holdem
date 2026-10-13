"""Installable Statue Fracture addon. Source lives in this package only."""
bl_info = {
    'name': 'Statue Fracture', 'author': 'Project Tools', 'version': (1, 4, 0),
    'blender': (4, 5, 0), 'location': 'View3D > Sidebar > Destruction',
    'description': 'Preserve statue surfaces, bake rigid VAT and export to Unity',
    'category': 'Object',
}

import bpy
from bpy.props import StringProperty
from . import fracture


class SF_AddonPreferences(bpy.types.AddonPreferences):
    bl_idname = __package__
    unity_project: StringProperty(name='Unity 项目', subtype='DIR_PATH',
        description='选择包含 Assets 和 ProjectSettings 的 Unity 项目根目录')
    unity_assets_folder: StringProperty(name='Unity 资源目录',
        default='Assets/Art/Models/Creatures/DragonGargoyle/VAT',
        description='相对于 Unity 项目的路径，必须在 Assets 内')

    def draw(self, context):
        self.layout.prop(self, 'unity_project')
        self.layout.prop(self, 'unity_assets_folder')
        self.layout.label(text='3D 视图 > N 面板 > Destruction；导出后 Unity 自动更新 VAT 资源')


def register():
    bpy.utils.register_class(SF_AddonPreferences)
    fracture.register()


def unregister():
    fracture.unregister()
    bpy.utils.unregister_class(SF_AddonPreferences)
