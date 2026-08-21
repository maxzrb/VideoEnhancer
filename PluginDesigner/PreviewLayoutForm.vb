Imports System
Imports System.Windows.Forms

Namespace PluginLayoutDesigner

    ''' <summary>
    ''' 实时预览页的布局设计器窗体（对应 VideoEnhancerPlugin\PluginPanel.vb 的 BuildPreviewPage）。
    ''' 用标准 WinForms 控件（Label / ComboBox / PictureBox）替代 LakeUI 自绘控件，
    ''' 保证 Visual Studio 设计器可以正常打开、拖拽。坐标与真实代码一一对应。
    '''
    ''' 用法：
    '''   1. VS 打开 PluginLayoutDesigner.vbproj，双击 PreviewLayoutForm.vb 进入设计视图；
    '''      本窗体全部使用绝对坐标（Location/Size，无 Dock），可以像 PluginLayoutForm
    '''      一样直接在 VS 里拖动控件、在属性窗口改数值。
    '''   2. 调整后把「控件名 (x, y) 宽x高」发给开发人员移植回 PluginPanel.vb。
    '''   编译出的程序仅用于查看布局，不包含任何运行时调整功能。
    ''' </summary>
    Public Class PreviewLayoutForm

        Public Sub New()
            InitializeComponent()
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ' 布局请在 VS 设计视图中调整；运行时不提供控件移动功能。
        End Sub

        Private Sub lblStatus_Click(sender As Object, e As EventArgs) Handles lblStatus.Click

        End Sub
    End Class

End Namespace