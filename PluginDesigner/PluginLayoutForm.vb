Imports System
Imports System.Windows.Forms

Namespace PluginLayoutDesigner

    ''' <summary>
    ''' 插件页面（视频超分）的布局设计器窗体。
    ''' 用标准 WinForms 控件替代 LakeUI 自绘控件（ModernComboBox / BooleanSwitch / HtmlColorLabel），
    ''' 保证 Visual Studio 设计器可以正常打开、拖拽。坐标与 PluginPanel.vb 的 InitializeUi() 一一对应。
    '''
    ''' 用法：
    '''   1. VS 打开 PluginLayoutDesigner.vbproj，双击 PluginLayoutForm.vb 进入设计视图；
    '''      本窗体全部使用绝对坐标（Location/Size，无 Dock），可以直接在 VS 里拖动控件。
    '''   2. 调整后把「控件名 (x, y) 宽x高」发给开发人员移植回 PluginPanel.vb。
    '''   编译出的程序仅用于查看布局，不包含任何运行时调整功能。
    ''' </summary>
    Public Class PluginLayoutForm

        Public Sub New()
            InitializeComponent()
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ' 布局请在 VS 设计视图中调整；运行时不提供控件移动功能。
        End Sub

        Private Sub lblInterp_Click(sender As Object, e As EventArgs) Handles lblInterp.Click

        End Sub

        Private Sub chkInterp_CheckedChanged(sender As Object, e As EventArgs) Handles chkInterp.CheckedChanged

        End Sub

        Private Sub lblMaster_Click(sender As Object, e As EventArgs) Handles lblMaster.Click

        End Sub

        Private Sub lblFactor_Click(sender As Object, e As EventArgs) Handles lblFactor.Click

        End Sub

        Private Sub Label3_Click(sender As Object, e As EventArgs) Handles Label3.Click

        End Sub

        Private Sub Label4_Click(sender As Object, e As EventArgs) Handles Label4.Click

        End Sub
    End Class

End Namespace
