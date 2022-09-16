using System;
using System.IO;
using System.Numerics;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterTex;
using Penumbra.Import.Textures;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private readonly Texture         _left  = new();
    private readonly Texture         _right = new();
    private readonly CombinedTexture _center;

    private readonly FileDialogManager _dialogManager    = ConfigWindow.SetupFileManager();
    private          bool              _overlayCollapsed = true;
    private          DXGIFormat        _currentFormat = DXGIFormat.R8G8B8A8UNorm;

    private void DrawInputChild( string label, Texture tex, Vector2 size, Vector2 imageSize )
    {
        using var child = ImRaii.Child( label, size, true );
        if( !child )
        {
            return;
        }

        using var id = ImRaii.PushId( label );
        ImGuiUtil.DrawTextButton( label, new Vector2( -1, 0 ), ImGui.GetColorU32( ImGuiCol.FrameBg ) );
        ImGui.NewLine();

        tex.PathInputBox( "##input", "Import Image...", "Can import game paths as well as your own files.", _mod!.ModPath.FullName,
            _dialogManager );

        if( tex == _left )
        {
            _center.DrawMatrixInputLeft( size.X );
        }
        else
        {
            _center.DrawMatrixInputRight( size.X );
        }

        ImGui.NewLine();
        tex.Draw( imageSize );
    }

    private void DrawOutputChild( Vector2 size, Vector2 imageSize )
    {
        using var child = ImRaii.Child( "Output", size, true );
        if( !child )
        {
            return;
        }

        if( _center.IsLoaded )
        {
            if( ImGui.Button( "Save as TEX", -Vector2.UnitX ) )
            {
                var fileName = Path.GetFileNameWithoutExtension( _left.Path.Length > 0 ? _left.Path : _right.Path );
                _dialogManager.SaveFileDialog( "Save Texture as TEX...", ".tex", fileName, ".tex", ( a, b ) => { }, _mod!.ModPath.FullName );
            }

            if( ImGui.Button( "Save as DDS", -Vector2.UnitX ) )
            {
                var fileName = Path.GetFileNameWithoutExtension( _right.Path.Length > 0 ? _right.Path : _left.Path );
                _dialogManager.SaveFileDialog( "Save Texture as DDS...", ".dds", fileName, ".dds", ( a, b ) => { if( a ) _center.SaveAsDDS( b, _currentFormat, false ); }, _mod!.ModPath.FullName );
            }

            if( ImGui.Button( "Save as PNG", -Vector2.UnitX ) )
            {
                var fileName = Path.GetFileNameWithoutExtension( _right.Path.Length > 0 ? _right.Path : _left.Path );
                _dialogManager.SaveFileDialog( "Save Texture as PNG...", ".png", fileName, ".png", ( a, b ) => { if (a) _center.SaveAsPng( b ); }, _mod!.ModPath.FullName );
            }

            ImGui.NewLine();
        }

        _center.Draw( imageSize );
    }

    private Vector2 GetChildWidth()
    {
        var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - ImGui.GetTextLineHeight();
        if( _overlayCollapsed )
        {
            var width = windowWidth - ImGui.GetStyle().FramePadding.X * 3;
            return new Vector2( width / 2, -1 );
        }

        return new Vector2( ( windowWidth - ImGui.GetStyle().FramePadding.X * 5 ) / 3, -1 );
    }

    private void DrawTextureTab()
    {
        _dialogManager.Draw();

        using var tab = ImRaii.TabItem( "Texture Import/Export (WIP)" );
        if( !tab )
        {
            return;
        }

        try
        {
            var childWidth = GetChildWidth();
            var imageSize  = new Vector2( childWidth.X - ImGui.GetStyle().FramePadding.X * 2 );
            DrawInputChild( "Input Texture", _left, childWidth, imageSize );
            ImGui.SameLine();
            DrawOutputChild( childWidth, imageSize );
            if( !_overlayCollapsed )
            {
                ImGui.SameLine();
                DrawInputChild( "Overlay Texture", _right, childWidth, imageSize );
            }

            ImGui.SameLine();
            DrawOverlayCollapseButton();
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Unknown Error while drawing textures:\n{e}" );
        }
    }

    private void DrawOverlayCollapseButton()
    {
        var (label, tooltip) = _overlayCollapsed
            ? ( ">", "Show a third panel in which you can import an additional texture as an overlay for the primary texture." )
            : ( "<", "Hide the overlay texture panel and clear the currently loaded overlay texture, if any." );
        if( ImGui.Button( label, new Vector2( ImGui.GetTextLineHeight(), ImGui.GetContentRegionAvail().Y ) ) )
        {
            _overlayCollapsed = !_overlayCollapsed;
        }

        ImGuiUtil.HoverTooltip( tooltip );
    }
}