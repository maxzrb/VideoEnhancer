Imports System
Imports System.Diagnostics
Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports LakeUI

Namespace videoenhancer

    ''' <summary>保留 LakeUI 滚动条，将滚轮输入合并为短时动画，并按外到内刷新滚动子树。</summary>
    Friend Class SmoothScrollPanel
        Inherits DpiLayoutPanel

        Private Const ScrollDurationMilliseconds As Double = 120.0
        Private ReadOnly _scrollTimer As New Timer With {.Interval = 16}
        Private _scrollAnimating As Boolean
        Private _scrollStart As Integer
        Private _scrollTarget As Integer
        Private _scrollStartedAt As Long
        Private _wheelRemainder As Double

        Public Sub New()
            AddHandler _scrollTimer.Tick, AddressOf OnScrollTimerTick
        End Sub

        Private Sub ApplyScrollPosition(horizontalOffset As Integer, verticalOffset As Integer)
            ' 位置移动和透明子树重新取景在同一个提交中完成，避免下一拍沿用旧背景。
            Using update = D3D_PaintBridge.BeginRenderUpdate(Me)
                MyBase.ScrollTo(horizontalOffset, verticalOffset)
            End Using
        End Sub

        Friend Function MaximumVerticalOffset() As Integer
            Dim contentBottom = DisplayRectangle.Top
            For Each child As Control In Controls
                If child.Visible Then contentBottom = Math.Max(contentBottom, child.Bottom + VerticalScrollOffset)
            Next
            Return Math.Max(0, contentBottom - DisplayRectangle.Bottom)
        End Function

        Friend Sub BeginWheelScroll(delta As Integer)
            If IsDisposed OrElse Disposing OrElse delta = 0 Then Return
            Dim maximum = MaximumVerticalOffset()
            If maximum <= 0 Then
                StopScrollAnimation()
                Return
            End If

            ' 保留不足一个像素的输入，触摸板的小 delta 不会被逐次取整吞掉。
            _wheelRemainder -= CDbl(delta) * ScaleY(VerticalScrollStep) / 120.0
            Dim distance = Math.Truncate(_wheelRemainder)
            _wheelRemainder -= distance
            If distance = 0 Then Return
            ' 反向输入立即从当前画面转向，不继续消化上一方向尚未执行的距离。
            Dim sameDirection = Math.Sign(distance) = Math.Sign(_scrollTarget - VerticalScrollOffset)
            Dim origin = If(_scrollAnimating AndAlso sameDirection, _scrollTarget, VerticalScrollOffset)
            _scrollTarget = CInt(Math.Clamp(CDbl(origin) + distance, 0.0, CDbl(maximum)))
            _scrollStart = VerticalScrollOffset
            If _scrollStart = _scrollTarget Then
                StopScrollAnimation()
                Return
            End If
            _scrollStartedAt = Stopwatch.GetTimestamp()
            _scrollAnimating = True
            _scrollTimer.Start()
        End Sub

        Friend Sub AdvanceScroll(elapsedMilliseconds As Double)
            If Not _scrollAnimating OrElse IsDisposed OrElse Disposing Then Return
            Dim maximum = MaximumVerticalOffset()
            _scrollTarget = Math.Min(_scrollTarget, maximum)
            Dim progress = Math.Clamp(elapsedMilliseconds / ScrollDurationMilliseconds, 0.0, 1.0)
            Dim eased = 1.0 - Math.Pow(1.0 - progress, 3.0)
            Dim position = Math.Clamp(CInt(Math.Round(_scrollStart + (_scrollTarget - _scrollStart) * eased)), 0, maximum)
            If position <> VerticalScrollOffset Then ApplyScrollPosition(HorizontalScrollOffset, position)
            If progress >= 1.0 OrElse position = _scrollTarget Then StopScrollAnimation(False)
        End Sub

        Private Sub OnScrollTimerTick(sender As Object, e As EventArgs)
            AdvanceScroll(Stopwatch.GetElapsedTime(_scrollStartedAt).TotalMilliseconds)
        End Sub

        Private Sub StopScrollAnimation(Optional resetRemainder As Boolean = True)
            _scrollAnimating = False
            If _scrollTimer IsNot Nothing Then _scrollTimer.Stop()
            If resetRemainder Then _wheelRemainder = 0.0
        End Sub

        Public Shadows Sub ScrollTo(horizontalOffset As Integer, verticalOffset As Integer)
            StopScrollAnimation()
            ' LakeUI 在计算滚动条几何后才钳制偏移，先限制输入以避免超范围计算。
            ApplyScrollPosition(Math.Max(0, horizontalOffset), Math.Clamp(verticalOffset, 0, MaximumVerticalOffset()))
        End Sub

        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            If (ModifierKeys And Keys.Shift) = Keys.Shift OrElse MaximumVerticalOffset() <= 0 Then
                StopScrollAnimation()
                Using update = D3D_PaintBridge.BeginRenderUpdate(Me)
                    MyBase.OnMouseWheel(e)
                End Using
                Return
            End If
            BeginWheelScroll(e.Delta)
            Dim handled = TryCast(e, HandledMouseEventArgs)
            If handled IsNot Nothing Then handled.Handled = True
        End Sub

        Friend Shared Function ForwardWheelToScrollHost(control As Control, delta As Integer) As Boolean
            Dim ancestor = If(control Is Nothing, Nothing, control.Parent)
            While ancestor IsNot Nothing
                Dim viewport = TryCast(ancestor, SmoothScrollPanel)
                If viewport IsNot Nothing Then
                    viewport.BeginWheelScroll(delta)
                    Return True
                End If
                ancestor = ancestor.Parent
            End While
            Return False
        End Function

        Friend Shared Function WheelDelta(wParam As IntPtr) As Integer
            Dim delta = CInt((wParam.ToInt64() >> 16) And &HFFFFL)
            Return If(delta >= &H8000, delta - &H10000, delta)
        End Function

        Protected Overrides Sub OnControlAdded(e As ControlEventArgs)
            MyBase.OnControlAdded(e)
            AddHandler e.Control.LocationChanged, AddressOf OnContentMoved
        End Sub

        Protected Overrides Sub OnControlRemoved(e As ControlEventArgs)
            RemoveHandler e.Control.LocationChanged, AddressOf OnContentMoved
            MyBase.OnControlRemoved(e)
        End Sub

        Private Sub OnContentMoved(sender As Object, e As EventArgs)
            If IsDisposed OrElse Disposing Then Return
            ' 只重绘当前视口及子树；不使宿主背景或其他页签失效，也不逐控件同步 Refresh。
            OuterToInnerRefreshScheduler.RequestFull(Me, invalidateChildren:=True)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            StopScrollAnimation()
            Using update = D3D_PaintBridge.BeginRenderUpdate(Me)
                MyBase.OnMouseDown(e)
            End Using
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            If Capture AndAlso (e.Button And MouseButtons.Left) <> MouseButtons.None Then
                ' 拖动滚动条同样会移动内容；普通悬停不触发整棵子树提交。
                Using update = D3D_PaintBridge.BeginRenderUpdate(Me)
                    MyBase.OnMouseMove(e)
                End Using
            Else
                MyBase.OnMouseMove(e)
            End If
        End Sub

        Protected Overrides Sub OnSizeChanged(e As EventArgs)
            StopScrollAnimation()
            MyBase.OnSizeChanged(e)
        End Sub

        Protected Overrides Sub OnVisibleChanged(e As EventArgs)
            If Not Visible Then StopScrollAnimation()
            MyBase.OnVisibleChanged(e)
        End Sub

        Protected Overrides Sub ScaleControl(factor As SizeF, specified As BoundsSpecified)
            StopScrollAnimation()
            MyBase.ScaleControl(factor, specified)
        End Sub

        Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
            StopScrollAnimation()
            MyBase.OnHandleDestroyed(e)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _scrollTimer IsNot Nothing Then
                StopScrollAnimation()
                RemoveHandler _scrollTimer.Tick, AddressOf OnScrollTimerTick
                _scrollTimer.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class

    ''' <summary>滚动内容使用新位置重新绘制，禁止 Windows 将旧背景像素搬到新位置。</summary>
    Friend Class GpuScrollContentPanel
        Inherits DpiLayoutPanel

        Private Const WmWindowPosChanging As Integer = &H46
        Private Const SwpNoMove As UInteger = &H2UI
        Private Const SwpNoCopyBits As UInteger = &H100UI

        <StructLayout(LayoutKind.Sequential)>
        Private Structure WindowPosition
            Public Handle As IntPtr
            Public InsertAfter As IntPtr
            Public X As Integer
            Public Y As Integer
            Public Width As Integer
            Public Height As Integer
            Public Flags As UInteger
        End Structure

        Friend Shared Function ScrollMoveFlags(flags As UInteger) As UInteger
            Return If((flags And SwpNoMove) = 0UI, flags Or SwpNoCopyBits, flags)
        End Function

        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = WmWindowPosChanging AndAlso m.LParam <> IntPtr.Zero Then
                Dim position = Marshal.PtrToStructure(Of WindowPosition)(m.LParam)
                position.Flags = ScrollMoveFlags(position.Flags)
                Marshal.StructureToPtr(position, m.LParam, False)
            End If
            MyBase.WndProc(m)
        End Sub
    End Class

End Namespace
