using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
#endif

namespace FS.Rendering
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class VFXDropDownAttribute : Attribute {}

    public static class VFXAssets
    {
#if UNITY_EDITOR
        public static IEnumerable<GameObject> GetVFXAssets()
        {
            // Get all prefabs in project with VFXController component
            var vfxControllers = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in vfxControllers)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.TryGetComponent<VFXController>(out var controller))
                    yield return prefab;
            }
        }
#endif
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Attribute processor that swaps out VFXDropDown with a ValueDropDown calling GetVFXAssets for GameObjects
    /// </summary>
    public class VFXDropDownAttributeProcessor : OdinAttributeProcessor<GameObject>
    {
        public override bool CanProcessSelfAttributes(InspectorProperty property) =>
            property.ValueEntry.TypeOfValue == typeof(GameObject);

        public override void ProcessSelfAttributes(InspectorProperty property, List<Attribute> attributes)
        {
            // Replace this attribute (VFXDropDown) with a ValueDropDown calling to the correct getter
            attributes.RemoveAll(a => a is VFXDropDownAttribute);
            attributes.Add(new ValueDropdownAttribute("@VFXAssets.GetVFXAssets()"));
        }
    }
    
    /// <summary>
    /// Value drop down editor for VFX Controllers with the VFXDropDown Attribute Editor
    /// </summary>
    public class VFXDropDownAttributeEditor : OdinAttributeDrawer<VFXDropDownAttribute, VFXController>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var value = ValueEntry.SmartValue;
            string btnLabel = "Select VFX";
            if (value) btnLabel = value.gameObject.name;

            OdinSelector<GameObject>.DrawSelectorDropdown(label, btnLabel, ShowSelector);
        }

        private OdinSelector<GameObject> ShowSelector(Rect rect)
        {
            var vfxControllers = VFXAssets.GetVFXAssets();
            var selector = new GenericSelector<GameObject>(vfxControllers);

            // Set icon to asset preview
            foreach (var menuItem in selector.SelectionTree.MenuItems)
            {
                menuItem.Icon = AssetPreview.GetAssetPreview(menuItem.Value as GameObject);
            }
            
            var currentSelection = ValueEntry.SmartValue;
            selector.SetSelection(currentSelection == null ? null : currentSelection.gameObject);

            selector.SelectionConfirmed += SelectionChanged;

            selector.ShowInPopup(rect);
            
            return selector;
        }

        private void SelectionChanged(IEnumerable<GameObject> obj)
        {
            if (obj == null) return;

            var first = obj.FirstOrDefault();
            if (first == null) return;
            
            ValueEntry.SmartValue = first.GetComponent<VFXController>();
        }
    }
#endif    
}