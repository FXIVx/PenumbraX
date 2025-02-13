using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private const int MdlMaterialMaximum = 4;

    private readonly FileEditor<MdlTab> _modelTab;

    private          string           _modelNewMaterial           = string.Empty;
    private readonly List<TagButtons> _subMeshAttributeTagWidgets = [];

    private bool DrawModelPanel(MdlTab tab, bool disabled)
    {
        var file = tab.Mdl;

        var subMeshTotal = file.Meshes.Aggregate(0, (count, mesh) => count + mesh.SubMeshCount);
        if (_subMeshAttributeTagWidgets.Count != subMeshTotal)
        {
            _subMeshAttributeTagWidgets.Clear();
            _subMeshAttributeTagWidgets.AddRange(
                Enumerable.Range(0, subMeshTotal).Select(_ => new TagButtons())
            );
        }

        var ret = false;

        ret |= DrawModelMaterialDetails(tab, disabled);

        if (ImGui.CollapsingHeader($"Meshes ({file.Meshes.Length})###meshes"))
            for (var i = 0; i < file.LodCount; ++i)
                ret |= DrawModelLodDetails(tab, i, disabled);

        ret |= DrawOtherModelDetails(file, disabled);

        return !disabled && ret;
    }

    private bool DrawModelMaterialDetails(MdlTab tab, bool disabled)
    {
        if (!ImGui.CollapsingHeader("Materials"))
            return false;

        using var table = ImRaii.Table(string.Empty, disabled ? 2 : 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return false;

        var ret       = false;
        var materials = tab.Mdl.Materials;

        ImGui.TableSetupColumn("index", ImGuiTableColumnFlags.WidthFixed,   80 * UiHelpers.Scale);
        ImGui.TableSetupColumn("path",  ImGuiTableColumnFlags.WidthStretch, 1);
        if (!disabled)
            ImGui.TableSetupColumn("actions", ImGuiTableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);

        var inputFlags = disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None;
        for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            ret |= DrawMaterialRow(tab, disabled, materials, materialIndex, inputFlags);

        if (materials.Length >= MdlMaterialMaximum || disabled)
            return ret;

        ImGui.TableNextColumn();

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##newMaterial", "Add new material...", ref _modelNewMaterial, Utf8GamePath.MaxGamePathLength, inputFlags);
        var validName = _modelNewMaterial.Length > 0 && _modelNewMaterial[0] == '/';
        ImGui.TableNextColumn();
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), UiHelpers.IconButtonSize, string.Empty, !validName, true))
            return ret;

        tab.Mdl.Materials = materials.AddItem(_modelNewMaterial);
        _modelNewMaterial = string.Empty;
        return true;
    }

    private bool DrawMaterialRow(MdlTab tab, bool disabled, string[] materials, int materialIndex, ImGuiInputTextFlags inputFlags)
    {
        using var id  = ImRaii.PushId(materialIndex);
        var       ret = false;
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"Material #{materialIndex + 1}");

        var temp = materials[materialIndex];
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText($"##material{materialIndex}", ref temp, Utf8GamePath.MaxGamePathLength, inputFlags)
         && temp.Length > 0
         && temp != materials[materialIndex]
           )
        {
            materials[materialIndex] = temp;
            ret                      = true;
        }

        if (disabled)
            return ret;

        ImGui.TableNextColumn();

        // Need to have at least one material.
        if (materials.Length <= 1)
            return ret;

        var tt             = "Delete this material.\nAny meshes targeting this material will be updated to use material #1.";
        var modifierActive = _config.DeleteModModifier.IsActive();
        if (!modifierActive)
            tt += $"\nHold {_config.DeleteModModifier} to delete.";
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize, tt, !modifierActive, true))
            return ret;

        tab.RemoveMaterial(materialIndex);
        return true;
    }

    private bool DrawModelLodDetails(MdlTab tab, int lodIndex, bool disabled)
    {
        using var lodNode = ImRaii.TreeNode($"Level of Detail #{lodIndex + 1}", ImGuiTreeNodeFlags.DefaultOpen);
        if (!lodNode)
            return false;

        var lod = tab.Mdl.Lods[lodIndex];
        var ret = false;

        for (var meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
            ret |= DrawModelMeshDetails(tab, lod.MeshIndex + meshOffset, disabled);

        return ret;
    }

    private bool DrawModelMeshDetails(MdlTab tab, int meshIndex, bool disabled)
    {
        using var meshNode = ImRaii.TreeNode($"Mesh #{meshIndex + 1}", ImGuiTreeNodeFlags.DefaultOpen);
        if (!meshNode)
            return false;

        using var id    = ImRaii.PushId(meshIndex);
        using var table = ImRaii.Table(string.Empty, 2, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return false;

        ImGui.TableSetupColumn("name",  ImGuiTableColumnFlags.WidthFixed,   100 * UiHelpers.Scale);
        ImGui.TableSetupColumn("field", ImGuiTableColumnFlags.WidthStretch, 1);

        var file = tab.Mdl;
        var mesh = file.Meshes[meshIndex];

        // Mesh material
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Material");

        ImGui.TableNextColumn();
        var ret = DrawMaterialCombo(tab, meshIndex, disabled);

        // Sub meshes
        for (var subMeshOffset = 0; subMeshOffset < mesh.SubMeshCount; subMeshOffset++)
            ret |= DrawSubMeshAttributes(tab, meshIndex, disabled, subMeshOffset);

        return ret;
    }

    private static bool DrawMaterialCombo(MdlTab tab, int meshIndex, bool disabled)
    {
        var       mesh = tab.Mdl.Meshes[meshIndex];
        using var _    = ImRaii.Disabled(disabled);
        ImGui.SetNextItemWidth(-1);
        using var materialCombo = ImRaii.Combo("##material", tab.Mdl.Materials[mesh.MaterialIndex]);

        if (!materialCombo)
            return false;

        var ret = false;
        foreach (var (material, materialIndex) in tab.Mdl.Materials.WithIndex())
        {
            if (!ImGui.Selectable(material, mesh.MaterialIndex == materialIndex))
                continue;

            tab.Mdl.Meshes[meshIndex].MaterialIndex = (ushort)materialIndex;
            ret                                     = true;
        }

        return ret;
    }

    private bool DrawSubMeshAttributes(MdlTab tab, int meshIndex, bool disabled, int subMeshOffset)
    {
        using var _ = ImRaii.PushId(subMeshOffset);

        var mesh         = tab.Mdl.Meshes[meshIndex];
        var subMeshIndex = mesh.SubMeshIndex + subMeshOffset;

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"Attributes #{subMeshOffset + 1}");

        ImGui.TableNextColumn();
        var widget     = _subMeshAttributeTagWidgets[subMeshIndex];
        var attributes = tab.GetSubMeshAttributes(subMeshIndex);

        var tagIndex = widget.Draw(string.Empty, string.Empty, attributes,
            out var editedAttribute, !disabled);
        if (tagIndex < 0)
            return false;

        var oldName = tagIndex < attributes.Count ? attributes[tagIndex] : null;
        var newName = editedAttribute.Length > 0 ? editedAttribute : null;
        tab.UpdateSubMeshAttribute(subMeshIndex, oldName, newName);

        return true;
    }

    private static bool DrawOtherModelDetails(MdlFile file, bool _)
    {
        if (!ImGui.CollapsingHeader("Further Content"))
            return false;

        using (var table = ImRaii.Table("##data", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (table)
            {
                ImGuiUtil.DrawTableColumn("Version");
                ImGuiUtil.DrawTableColumn(file.Version.ToString());
                ImGuiUtil.DrawTableColumn("Radius");
                ImGuiUtil.DrawTableColumn(file.Radius.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("Model Clip Out Distance");
                ImGuiUtil.DrawTableColumn(file.ModelClipOutDistance.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("Shadow Clip Out Distance");
                ImGuiUtil.DrawTableColumn(file.ShadowClipOutDistance.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("LOD Count");
                ImGuiUtil.DrawTableColumn(file.LodCount.ToString());
                ImGuiUtil.DrawTableColumn("Enable Index Buffer Streaming");
                ImGuiUtil.DrawTableColumn(file.EnableIndexBufferStreaming.ToString());
                ImGuiUtil.DrawTableColumn("Enable Edge Geometry");
                ImGuiUtil.DrawTableColumn(file.EnableEdgeGeometry.ToString());
                ImGuiUtil.DrawTableColumn("Flags 1");
                ImGuiUtil.DrawTableColumn(file.Flags1.ToString());
                ImGuiUtil.DrawTableColumn("Flags 2");
                ImGuiUtil.DrawTableColumn(file.Flags2.ToString());
                ImGuiUtil.DrawTableColumn("Vertex Declarations");
                ImGuiUtil.DrawTableColumn(file.VertexDeclarations.Length.ToString());
                ImGuiUtil.DrawTableColumn("Bone Bounding Boxes");
                ImGuiUtil.DrawTableColumn(file.BoneBoundingBoxes.Length.ToString());
                ImGuiUtil.DrawTableColumn("Bone Tables");
                ImGuiUtil.DrawTableColumn(file.BoneTables.Length.ToString());
                ImGuiUtil.DrawTableColumn("Element IDs");
                ImGuiUtil.DrawTableColumn(file.ElementIds.Length.ToString());
                ImGuiUtil.DrawTableColumn("Extra LoDs");
                ImGuiUtil.DrawTableColumn(file.ExtraLods.Length.ToString());
                ImGuiUtil.DrawTableColumn("Meshes");
                ImGuiUtil.DrawTableColumn(file.Meshes.Length.ToString());
                ImGuiUtil.DrawTableColumn("Shape Meshes");
                ImGuiUtil.DrawTableColumn(file.ShapeMeshes.Length.ToString());
                ImGuiUtil.DrawTableColumn("LoDs");
                ImGuiUtil.DrawTableColumn(file.Lods.Length.ToString());
                ImGuiUtil.DrawTableColumn("Vertex Declarations");
                ImGuiUtil.DrawTableColumn(file.VertexDeclarations.Length.ToString());
                ImGuiUtil.DrawTableColumn("Stack Size");
                ImGuiUtil.DrawTableColumn(file.StackSize.ToString());
            }
        }

        using (var materials = ImRaii.TreeNode("Materials", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (materials)
                foreach (var material in file.Materials)
                    ImRaii.TreeNode(material, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var attributes = ImRaii.TreeNode("Attributes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (attributes)
                foreach (var attribute in file.Attributes)
                    ImRaii.TreeNode(attribute, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var bones = ImRaii.TreeNode("Bones", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (bones)
                foreach (var bone in file.Bones)
                    ImRaii.TreeNode(bone, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var shapes = ImRaii.TreeNode("Shapes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (shapes)
                foreach (var shape in file.Shapes)
                    ImRaii.TreeNode(shape.ShapeName, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        if (file.RemainingData.Length > 0)
        {
            using var t = ImRaii.TreeNode($"Additional Data (Size: {file.RemainingData.Length})###AdditionalData");
            if (t)
                ImGuiUtil.TextWrapped(string.Join(' ', file.RemainingData.Select(c => $"{c:X2}")));
        }

        return false;
    }
}
