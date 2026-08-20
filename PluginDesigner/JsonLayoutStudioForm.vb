Imports System.Drawing
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Windows.Forms

Namespace PluginLayoutDesigner

    Public Class JsonLayoutStudioForm
        Inherits Form

        Private Class LayoutDocument
            Public Property CanvasWidth As Integer = 900
            Public Property CanvasHeight As Integer = 620
            Public Property Controls As New List(Of LayoutItem)()
        End Class

        Private Class LayoutItem
            Public Property Name As String = "control"
            Public Property Type As String = "Label"
            Public Property Text As String = ""
            Public Property CenterX As Integer
            Public Property CenterY As Integer
            Public Property Width As Integer = 120
            Public Property Height As Integer = 32
            <JsonIgnore>
            Public Property View As Control
        End Class

        Private ReadOnly _canvas As New Panel()
        Private ReadOnly _list As New ListBox()
        Private ReadOnly _txtName As New TextBox()
        Private ReadOnly _txtType As New TextBox()
        Private ReadOnly _txtText As New TextBox()
        Private ReadOnly _numX As New NumericUpDown()
        Private ReadOnly _numY As New NumericUpDown()
        Private ReadOnly _numW As New NumericUpDown()
        Private ReadOnly _numH As New NumericUpDown()
        Private _doc As New LayoutDocument()
        Private _selected As LayoutItem
        Private _dragOffset As Point
        Private _dragging As Boolean

        Public Sub New()
            Text = "Plugin Layout Studio · JSON"
            ClientSize = New Size(1280, 760)
            MinimumSize = New Size(1000, 650)
            Font = New Font("Microsoft YaHei UI", 10.0F)
            BuildUi()
            AddSampleItems()
        End Sub

        Private Sub BuildUi()
            Dim toolbar As New FlowLayoutPanel() With {.Dock = DockStyle.Top, .Height = 44, .Padding = New Padding(6), .WrapContents = False}
            AddButton(toolbar, "添加标签", Sub() AddItem("Label"))
            AddButton(toolbar, "添加按钮", Sub() AddItem("Button"))
            AddButton(toolbar, "添加下拉框", Sub() AddItem("ComboBox"))
            AddButton(toolbar, "添加开关", Sub() AddItem("CheckBox"))
            AddButton(toolbar, "添加面板", Sub() AddItem("Panel"))
            AddButton(toolbar, "保存 JSON", Sub() SaveJson())
            AddButton(toolbar, "加载 JSON", Sub() LoadJson())
            Controls.Add(toolbar)

            Dim propertyPanel As New Panel() With {.Dock = DockStyle.Right, .Width = 300, .Padding = New Padding(12)}
            Controls.Add(propertyPanel)
            Dim y = 8
            AddField(propertyPanel, "控件名称", _txtName, y) : y += 42
            AddField(propertyPanel, "控件类型", _txtType, y) : y += 42
            AddField(propertyPanel, "显示文字", _txtText, y) : y += 42
            AddField(propertyPanel, "中心 X", _numX, y) : y += 42
            AddField(propertyPanel, "中心 Y", _numY, y) : y += 42
            AddField(propertyPanel, "宽度", _numW, y) : y += 42
            AddField(propertyPanel, "高度", _numH, y) : y += 42
            Dim hint As New Label() With {.Text = "所有坐标均相对左侧画布。中心点 X/Y 会转换为 WinForms Location。", .AutoSize = False, .Width = 270, .Height = 60, .Top = y + 8}
            propertyPanel.Controls.Add(hint)

            Dim split As New SplitContainer() With {.Dock = DockStyle.Fill, .SplitterDistance = 920, .FixedPanel = FixedPanel.Panel2}
            Controls.Add(split)
            split.Panel1.Padding = New Padding(12)
            _canvas.Dock = DockStyle.Fill
            _canvas.BackColor = Color.FromArgb(30, 32, 38)
            _canvas.BorderStyle = BorderStyle.FixedSingle
            split.Panel1.Controls.Add(_canvas)
            split.Panel2.Padding = New Padding(12)
            _list.Dock = DockStyle.Fill
            _list.DisplayMember = "Name"
            AddHandler _list.SelectedIndexChanged, AddressOf SelectItem
            split.Panel2.Controls.Add(_list)
            For Each input As Control In {_txtName, _txtType, _txtText, _numX, _numY, _numW, _numH}
                AddHandler input.TextChanged, AddressOf PropertiesChanged
            Next
            For Each input As NumericUpDown In {_numX, _numY, _numW, _numH}
                input.Minimum = -5000 : input.Maximum = 5000
            Next
        End Sub

        Private Sub AddButton(parent As FlowLayoutPanel, caption As String, action As Action)
            Dim b As New Button() With {.Text = caption, .AutoSize = True}
            AddHandler b.Click, Sub() action()
            parent.Controls.Add(b)
        End Sub

        Private Sub AddField(parent As Control, caption As String, editor As Control, top As Integer)
            Dim label As New Label() With {.Text = caption, .AutoSize = True, .Top = top + 4, .Left = 0}
            editor.Left = 92 : editor.Top = top : editor.Width = 180
            parent.Controls.Add(label) : parent.Controls.Add(editor)
        End Sub

        Private Sub AddSampleItems()
            AddItem("Label", "标题", 120, 50, 180, 36)
            AddItem("Button", "开始生成", 450, 520, 180, 42)
            AddItem("ComboBox", "编码器", 500, 120, 260, 38)
        End Sub

        Private Sub AddItem(typeName As String, Optional text As String = Nothing, Optional cx As Integer = 0, Optional cy As Integer = 0, Optional width As Integer = 160, Optional height As Integer = 36)
            Dim index = _doc.Controls.Count + 1
            Dim item As New LayoutItem With {.Name = typeName.ToLowerInvariant() & index.ToString(), .Type = typeName, .Text = If(text, typeName), .CenterX = If(cx = 0, 120 + index * 20, cx), .CenterY = If(cy = 0, 60 + index * 20, cy), .Width = width, .Height = height}
            _doc.Controls.Add(item)
            CreateView(item)
            _list.Items.Add(item)
            _list.SelectedItem = item
        End Sub

        Private Sub CreateView(item As LayoutItem)
            Dim view As Control
            Select Case item.Type
                Case "Button" : view = New Button()
                Case "ComboBox" : view = New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList}
                Case "CheckBox" : view = New CheckBox()
                Case "Panel" : view = New Panel() With {.BackColor = Color.FromArgb(55, 58, 68)}
                Case Else : view = New Label() With {.TextAlign = ContentAlignment.MiddleCenter, .BackColor = Color.FromArgb(48, 50, 58)}
            End Select
            view.Tag = item
            view.ForeColor = Color.White
            view.Text = item.Text
            view.Size = New Size(item.Width, item.Height)
            view.Location = New Point(item.CenterX - item.Width \ 2, item.CenterY - item.Height \ 2)
            AddHandler view.MouseDown, AddressOf ViewMouseDown
            AddHandler view.MouseMove, AddressOf ViewMouseMove
            AddHandler view.MouseUp, AddressOf ViewMouseUp
            AddHandler view.Click, Sub() _list.SelectedItem = item
            item.View = view
            _canvas.Controls.Add(view)
        End Sub

        Private Sub ViewMouseDown(sender As Object, e As MouseEventArgs)
            If e.Button <> MouseButtons.Left Then Return
            Dim view = DirectCast(sender, Control)
            _list.SelectedItem = view.Tag
            _dragging = True : _dragOffset = e.Location
        End Sub

        Private Sub ViewMouseMove(sender As Object, e As MouseEventArgs)
            If Not _dragging Then Return
            Dim view = DirectCast(sender, Control)
            view.Left += e.X - _dragOffset.X : view.Top += e.Y - _dragOffset.Y
            SyncItemFromView(DirectCast(view.Tag, LayoutItem))
            RefreshProperties()
        End Sub

        Private Sub ViewMouseUp(sender As Object, e As MouseEventArgs)
            _dragging = False
        End Sub

        Private Sub SelectItem(sender As Object, e As EventArgs)
            _selected = TryCast(_list.SelectedItem, LayoutItem)
            RefreshProperties()
        End Sub

        Private Sub RefreshProperties()
            If _selected Is Nothing Then Return
            RemovePropertyHandlers()
            _txtName.Text = _selected.Name : _txtType.Text = _selected.Type : _txtText.Text = _selected.Text
            _numX.Value = _selected.CenterX : _numY.Value = _selected.CenterY : _numW.Value = _selected.Width : _numH.Value = _selected.Height
            AddPropertyHandlers()
        End Sub

        Private Sub PropertiesChanged(sender As Object, e As EventArgs)
            If _selected Is Nothing Then Return
            _selected.Name = _txtName.Text : _selected.Text = _txtText.Text
            _selected.CenterX = CInt(_numX.Value) : _selected.CenterY = CInt(_numY.Value)
            _selected.Width = Math.Max(1, CInt(_numW.Value)) : _selected.Height = Math.Max(1, CInt(_numH.Value))
            _selected.View.Text = _selected.Text
            _selected.View.Size = New Size(_selected.Width, _selected.Height)
            _selected.View.Location = New Point(_selected.CenterX - _selected.Width \ 2, _selected.CenterY - _selected.Height \ 2)
            _list.Refresh()
        End Sub

        Private Sub SyncItemFromView(item As LayoutItem)
            item.CenterX = item.View.Left + item.View.Width \ 2 : item.CenterY = item.View.Top + item.View.Height \ 2
        End Sub

        Private Sub AddPropertyHandlers()
            For Each input As Control In {_txtName, _txtType, _txtText, _numX, _numY, _numW, _numH} : AddHandler input.TextChanged, AddressOf PropertiesChanged : Next
        End Sub
        Private Sub RemovePropertyHandlers()
            For Each input As Control In {_txtName, _txtType, _txtText, _numX, _numY, _numW, _numH} : RemoveHandler input.TextChanged, AddressOf PropertiesChanged : Next
        End Sub

        Private Sub SaveJson()
            Using dlg As New SaveFileDialog() With {.Filter = "布局 JSON (*.json)|*.json", .FileName = "videoenhancer-layout.json"}
                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                Dim options As New JsonSerializerOptions With {.WriteIndented = True}
                System.IO.File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_doc, options), System.Text.Encoding.UTF8)
            End Using
        End Sub

        Private Sub LoadJson()
            Using dlg As New OpenFileDialog() With {.Filter = "布局 JSON (*.json)|*.json"}
                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                _doc = JsonSerializer.Deserialize(Of LayoutDocument)(System.IO.File.ReadAllText(dlg.FileName))
                _canvas.Controls.Clear() : _list.Items.Clear()
                For Each item In _doc.Controls : CreateView(item) : _list.Items.Add(item) : Next
                If _list.Items.Count > 0 Then _list.SelectedIndex = 0
            End Using
        End Sub
    End Class
End Namespace
