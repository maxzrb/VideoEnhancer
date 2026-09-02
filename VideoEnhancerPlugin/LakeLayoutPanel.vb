Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Reflection
Imports System.Windows.Forms
Imports LakeUI

Namespace videoenhancer

    ''' <summary>兼容旧 ContentAlignment 调用点的 LakeUI 文本标签。</summary>
    Friend Class LakeTextLabel
        Inherits HtmlColorLabel

        Public Shadows Property TextAlign As ContentAlignment
            Get
                Select Case MyBase.TextAlign
                    Case HtmlColorLabel.TextAlignEnum.TopLeft : Return ContentAlignment.TopLeft
                    Case HtmlColorLabel.TextAlignEnum.TopRight : Return ContentAlignment.TopRight
                    Case HtmlColorLabel.TextAlignEnum.MiddleLeft : Return ContentAlignment.MiddleLeft
                    Case HtmlColorLabel.TextAlignEnum.Center : Return ContentAlignment.MiddleCenter
                    Case HtmlColorLabel.TextAlignEnum.MiddleRight : Return ContentAlignment.MiddleRight
                    Case HtmlColorLabel.TextAlignEnum.BottomLeft : Return ContentAlignment.BottomLeft
                    Case HtmlColorLabel.TextAlignEnum.BottomRight : Return ContentAlignment.BottomRight
                    Case Else : Return ContentAlignment.MiddleLeft
                End Select
            End Get
            Set(value As ContentAlignment)
                Dim mapped As HtmlColorLabel.TextAlignEnum
                Select Case value
                    Case ContentAlignment.TopLeft : mapped = HtmlColorLabel.TextAlignEnum.TopLeft
                    Case ContentAlignment.TopRight : mapped = HtmlColorLabel.TextAlignEnum.TopRight
                    Case ContentAlignment.MiddleLeft : mapped = HtmlColorLabel.TextAlignEnum.MiddleLeft
                    Case ContentAlignment.MiddleCenter : mapped = HtmlColorLabel.TextAlignEnum.Center
                    Case ContentAlignment.MiddleRight : mapped = HtmlColorLabel.TextAlignEnum.MiddleRight
                    Case ContentAlignment.BottomLeft : mapped = HtmlColorLabel.TextAlignEnum.BottomLeft
                    Case ContentAlignment.BottomRight : mapped = HtmlColorLabel.TextAlignEnum.BottomRight
                    Case Else : mapped = HtmlColorLabel.TextAlignEnum.MiddleLeft
                End Select
                MyBase.TextAlign = mapped
            End Set
        End Property
    End Class

    ''' <summary>
    ''' 修正 LakeUI 5.1+ ModernComboBox 在控件先以默认窄尺寸选中项目、随后由布局
    ''' 扩宽时保留旧横向滚动偏移的问题。只读下拉框应始终从文本开头显示选项。
    ''' </summary>
    Friend Class LakeComboBox
        Inherits ModernComboBox

        Private Shared ReadOnly TextRendererField As FieldInfo =
            GetType(ModernComboBox).GetField("_textRenderer",
                BindingFlags.Instance Or BindingFlags.NonPublic)

        Public Sub New()
            AddHandler SelectedIndexChanged, Sub(sender, e) ResetTextViewport()
            AddHandler TextChanged, Sub(sender, e) ResetTextViewport()
        End Sub

        Protected Overrides Sub OnSizeChanged(e As EventArgs)
            MyBase.OnSizeChanged(e)
            ResetTextViewport()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            ResetTextViewport()
        End Sub

        Private Sub ResetTextViewport()
            If Editable Then Return
            Try
                If TextRendererField Is Nothing Then Return
                Dim renderer = TextRendererField.GetValue(Me)
                If renderer Is Nothing Then Return
                Dim scrollField = renderer.GetType().GetField("_scrollXOffset",
                    BindingFlags.Instance Or BindingFlags.NonPublic)
                If scrollField IsNot Nothing Then scrollField.SetValue(renderer, 0)
            Catch
                ' LakeUI 内部字段变化时不影响下拉框的正常交互。
            End Try
        End Sub
    End Class

    ''' <summary>
    ''' LakeUI 原生网格容器。它只计算子控件边界，不绘制任何内容，
    ''' 因此背景、滚动条和 GPU 表面仍由 ModernPanel 统一处理。
    ''' </summary>
    Friend Class ModernGridPanel
        Inherits ModernPanel

        Private NotInheritable Class Placement
            Public Property Column As Integer
            Public Property Row As Integer
            Public Property ColumnSpan As Integer = 1
            Public Property RowSpan As Integer = 1
        End Class

        Private ReadOnly _placements As New Dictionary(Of Control, Placement)()

        Public Sub New()
            LayoutMode = ModernPanel.LayoutModeEnum.Absolute
            BackColor = Color.Transparent
            BackColor1 = Color.Transparent
            BorderSize = 0
            Margin = Padding.Empty
            Padding = Padding.Empty
        End Sub

        Public Property ColumnCount As Integer = 1
        Public Property RowCount As Integer = 1
        Public ReadOnly Property ColumnStyles As New List(Of ColumnStyle)()
        Public ReadOnly Property RowStyles As New List(Of RowStyle)()

        Public Sub AddAt(control As Control, column As Integer, row As Integer)
            If control Is Nothing Then Return
            Dim placement = GetOrCreatePlacement(control)
            placement.Column = Math.Max(0, column)
            placement.Row = Math.Max(0, row)
            control.Dock = DockStyle.None
            If Not Controls.Contains(control) Then Controls.Add(control)
            PerformLayout()
        End Sub

        Public Sub SetColumnSpan(control As Control, span As Integer)
            If control Is Nothing Then Return
            Dim placement = GetOrCreatePlacement(control)
            placement.ColumnSpan = Math.Max(1, span)
            If Not Controls.Contains(control) Then Controls.Add(control)
            PerformLayout()
        End Sub

        Public Sub SetRowSpan(control As Control, span As Integer)
            If control Is Nothing Then Return
            Dim placement = GetOrCreatePlacement(control)
            placement.RowSpan = Math.Max(1, span)
            If Not Controls.Contains(control) Then Controls.Add(control)
            PerformLayout()
        End Sub

        Private Function GetOrCreatePlacement(control As Control) As Placement
            Dim placement As Placement = Nothing
            If Not _placements.TryGetValue(control, placement) Then
                placement = New Placement()
                _placements(control) = placement
            End If
            Return placement
        End Function

        Protected Overrides Sub OnControlRemoved(e As ControlEventArgs)
            If e.Control IsNot Nothing AndAlso _placements IsNot Nothing Then _placements.Remove(e.Control)
            MyBase.OnControlRemoved(e)
        End Sub

        Private Shared Function ResolveColumnSizes(styles As List(Of ColumnStyle), count As Integer,
                                                   available As Integer) As Integer()
            Dim actualCount = Math.Max(1, count)
            Dim sizes(actualCount - 1) As Integer
            Dim fixedTotal As Single = 0
            Dim percentTotal As Single = 0
            For i As Integer = 0 To actualCount - 1
                Dim style = If(i < styles.Count, styles(i), New ColumnStyle(SizeType.Percent, 100.0F / actualCount))
                If style.SizeType = SizeType.Absolute Then
                    fixedTotal += Math.Max(0, style.Width)
                ElseIf style.SizeType = SizeType.Percent Then
                    percentTotal += Math.Max(0, style.Width)
                Else
                    percentTotal += 1.0F
                End If
            Next
            Dim remaining = Math.Max(0, available - CInt(Math.Round(fixedTotal)))
            Dim used As Integer = 0
            For i As Integer = 0 To actualCount - 1
                Dim style = If(i < styles.Count, styles(i), New ColumnStyle(SizeType.Percent, 100.0F / actualCount))
                Dim size As Integer
                If style.SizeType = SizeType.Absolute Then
                    size = Math.Max(0, CInt(Math.Round(style.Width)))
                Else
                    Dim weight = If(style.SizeType = SizeType.Percent, Math.Max(0, style.Width), 1.0F)
                    size = If(percentTotal > 0, CInt(Math.Round(remaining * weight / percentTotal)), 0)
                End If
                If i = actualCount - 1 Then size = Math.Max(0, available - used)
                sizes(i) = size
                used += size
            Next
            Return sizes
        End Function

        Private Shared Function ResolveRowSizes(styles As List(Of RowStyle), count As Integer,
                                                 available As Integer) As Integer()
            Dim actualCount = Math.Max(1, count)
            Dim sizes(actualCount - 1) As Integer
            Dim fixedTotal As Single = 0
            Dim percentTotal As Single = 0
            For i As Integer = 0 To actualCount - 1
                Dim style = If(i < styles.Count, styles(i), New RowStyle(SizeType.Percent, 100.0F / actualCount))
                If style.SizeType = SizeType.Absolute Then
                    fixedTotal += Math.Max(0, style.Height)
                ElseIf style.SizeType = SizeType.Percent Then
                    percentTotal += Math.Max(0, style.Height)
                Else
                    percentTotal += 1.0F
                End If
            Next
            Dim remaining = Math.Max(0, available - CInt(Math.Round(fixedTotal)))
            Dim used As Integer = 0
            For i As Integer = 0 To actualCount - 1
                Dim style = If(i < styles.Count, styles(i), New RowStyle(SizeType.Percent, 100.0F / actualCount))
                Dim size As Integer
                If style.SizeType = SizeType.Absolute Then
                    size = Math.Max(0, CInt(Math.Round(style.Height)))
                Else
                    Dim weight = If(style.SizeType = SizeType.Percent, Math.Max(0, style.Height), 1.0F)
                    size = If(percentTotal > 0, CInt(Math.Round(remaining * weight / percentTotal)), 0)
                End If
                If i = actualCount - 1 Then size = Math.Max(0, available - used)
                sizes(i) = size
                used += size
            Next
            Return sizes
        End Function

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            MyBase.OnLayout(levent)
            ' ModernPanel 的基类构造函数可能在派生字段初始化前触发布局。
            If _placements Is Nothing OrElse _placements.Count = 0 Then Return

            Dim display = DisplayRectangle
            Dim columns = ResolveColumnSizes(ColumnStyles, ColumnCount, display.Width)
            Dim rows = ResolveRowSizes(RowStyles, RowCount, display.Height)
            Dim columnOffsets(Math.Max(0, columns.Length - 1)) As Integer
            Dim rowOffsets(Math.Max(0, rows.Length - 1)) As Integer
            For i As Integer = 1 To columns.Length - 1
                columnOffsets(i) = columnOffsets(i - 1) + columns(i - 1)
            Next
            For i As Integer = 1 To rows.Length - 1
                rowOffsets(i) = rowOffsets(i - 1) + rows(i - 1)
            Next

            For Each pair In _placements
                Dim control = pair.Key
                If control.IsDisposed Then Continue For
                Dim placement = pair.Value
                Dim c = Math.Min(Math.Max(0, placement.Column), columns.Length - 1)
                Dim r = Math.Min(Math.Max(0, placement.Row), rows.Length - 1)
                Dim cEnd = Math.Min(columns.Length, c + Math.Max(1, placement.ColumnSpan))
                Dim rEnd = Math.Min(rows.Length, r + Math.Max(1, placement.RowSpan))
                Dim bounds = New Rectangle(display.Left + columnOffsets(c),
                                           display.Top + rowOffsets(r),
                                           Math.Max(0, columnOffsets(cEnd - 1) + columns(cEnd - 1) - columnOffsets(c)),
                                           Math.Max(0, rowOffsets(rEnd - 1) + rows(rEnd - 1) - rowOffsets(r)))
                Dim margin = control.Margin
                bounds = New Rectangle(bounds.Left + margin.Left,
                                       bounds.Top + margin.Top,
                                       Math.Max(0, bounds.Width - margin.Horizontal),
                                       Math.Max(0, bounds.Height - margin.Vertical))
                control.SetBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height)
            Next
        End Sub
    End Class

    ''' <summary>固定/比例列布局，用于 LakeUI 控件之间的横向字段行。</summary>
    Friend Class ModernHorizontalPanel
        Inherits ModernPanel

        Private ReadOnly _columns As Single()
        Private ReadOnly _columnByControl As New Dictionary(Of Control, Integer)()

        Public Sub New(ParamArray columns As Single())
            _columns = If(columns, Array.Empty(Of Single)())
            LayoutMode = ModernPanel.LayoutModeEnum.Absolute
            BackColor = Color.Transparent
            BackColor1 = Color.Transparent
            BorderSize = 0
            Margin = Padding.Empty
            Padding = Padding.Empty
        End Sub

        Public Sub AddColumn(control As Control, columnIndex As Integer)
            If control Is Nothing Then Return
            control.Dock = DockStyle.None
            _columnByControl(control) = Math.Max(0, columnIndex)
            If Not Controls.Contains(control) Then Controls.Add(control)
            PerformLayout()
        End Sub

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            MyBase.OnLayout(levent)
            ' ModernPanel 的基类构造函数可能在派生字段初始化前触发布局。
            If _columns Is Nothing OrElse _columns.Length = 0 OrElse
               _columnByControl Is Nothing OrElse _columnByControl.Count = 0 Then Return
            Dim availableWidth = Math.Max(0, DisplayRectangle.Width)
            Dim fixedWidth As Single = 0
            Dim totalWeight As Single = 0
            For Each column In _columns
                If column >= 0 Then fixedWidth += column Else totalWeight += -column
            Next
            Dim remaining = Math.Max(0.0F, availableWidth - fixedWidth)
            Dim widths(_columns.Length - 1) As Integer
            Dim used As Integer = 0
            For i As Integer = 0 To _columns.Length - 1
                Dim width = If(_columns(i) >= 0, _columns(i),
                              If(totalWeight > 0, remaining * (-_columns(i)) / totalWeight, 0.0F))
                If i = _columns.Length - 1 Then
                    widths(i) = Math.Max(0, availableWidth - used)
                Else
                    widths(i) = Math.Max(0, CInt(Math.Round(width)))
                End If
                used += widths(i)
            Next
            For Each pair In _columnByControl
                Dim index = Math.Min(Math.Max(0, pair.Value), widths.Length - 1)
                Dim left = DisplayRectangle.Left
                For i As Integer = 0 To index - 1
                    left += widths(i)
                Next
                Dim margin = pair.Key.Margin
                Dim width = Math.Max(0, widths(index) - margin.Horizontal)
                Dim height = Math.Max(0, DisplayRectangle.Height - margin.Vertical)
                If pair.Key.Anchor = AnchorStyles.None Then
                    width = Math.Min(width, Math.Max(0, pair.Key.Width))
                    height = Math.Min(height, Math.Max(0, pair.Key.Height))
                    pair.Key.SetBounds(left + margin.Left + Math.Max(0, (widths(index) - width - margin.Horizontal) \ 2),
                                       DisplayRectangle.Top + margin.Top + Math.Max(0, (DisplayRectangle.Height - height - margin.Vertical) \ 2),
                                       width, height)
                Else
                    pair.Key.SetBounds(left + margin.Left, DisplayRectangle.Top + margin.Top, width, height)
                End If
            Next
        End Sub
    End Class

End Namespace
