using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace FS.Rendering.Editor
{
    public class SectionHeaderDecorator : MaterialPropertyDrawer
    {
        protected string m_headerText;
        private const int k_headerHeight = 30;
        private const int k_headerPadding = 20;

        
        public SectionHeaderDecorator(string headerText)
        {
            m_headerText = headerText;
        }
        

        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
            position.y += k_headerPadding;
            
            // Draw the header background
            EditorGUI.DrawRect(new Rect(position.x, position.y - 2, position.width, 2), Color.black);
            EditorGUI.DrawRect(new Rect(position.x, position.y, position.width, k_headerHeight), new Color(0.3f, 0.1f, 0.3f));
            
            // Draw the header text
            position.height = EditorGUIUtility.singleLineHeight * 1.2f;
            EditorGUI.DropShadowLabel(position, m_headerText);
            //EditorGUI.LabelField(new Rect(position.x + 5, textCenter, position.width - 10, 20), m_headerText, EditorStyles.boldLabel);

            position.y += k_headerHeight;
            position.height = base.GetPropertyHeight(prop, label, editor);
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return k_headerHeight + k_headerPadding + base.GetPropertyHeight(prop, label, editor);
            return 20 + base.GetPropertyHeight(prop, label, editor);
        }
    }
    
    public class HideIfDrawer : MaterialPropertyDrawer
    {
        protected string[] argValue;
        protected bool bElementHidden;
        protected bool m_isToggle;

        //constructor permutations -- params doesn't seem to work for property drawer inputs :( -----------
        public HideIfDrawer(string name1)
        {
            argValue = new string[] { name1 };
        }

        public HideIfDrawer(string name1, string name2)
        {
            if (name2 == "Toggle")
            {
                m_isToggle = true;
                argValue = new string[] { name1 };
            }
            else
            {
                argValue = new string[] { name1, name2 };
            }
        }

        public HideIfDrawer(string name1, string name2, string name3)
        {
            argValue = new string[] { name1, name2, name3 };
        }

        public HideIfDrawer(string name1, string name2, string name3, string name4)
        {
            argValue = new string[] { name1, name2, name3, name4 };
        }

        //-------------------------------------------------------------------------------------------------

        public override void OnGUI (Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
            bElementHidden = false;
            
            for(int i=0; i<editor.targets.Length; i++)
            {
                //material object that we're targetting...
                Material mat = editor.targets[i] as Material;
                if(mat != null)
                {
                    //check for the dependencies:
                    for (int j = 0; j < argValue.Length; j++)
                    {
                        bElementHidden |= mat.IsKeywordEnabled(argValue[j]);
                    }
                }
            }

            if (!bElementHidden)
            {
                if (m_isToggle && prop.type == MaterialProperty.PropType.Float)
                    prop.floatValue = EditorGUILayout.Toggle(label, prop.floatValue > 0.0f) ? 1f : 0f;
                else 
                    editor.DefaultShaderProperty(prop, label);
            }
        }

        //We need to override the height so it's not adding any extra (unfortunately texture drawers will still add an extra bit of padding regardless):
        public override float GetPropertyHeight (MaterialProperty prop, string label, MaterialEditor editor)
        {
            //@TODO: manually standardise element compaction
            float height = base.GetPropertyHeight (prop, label, editor);
            return bElementHidden ? 0.0f : height-16;

            return 0;
        }
    }
    
    public class ShowIfDrawer : MaterialPropertyDrawer
    {
        protected string[] argValue;
        protected bool bElementHidden;
        protected bool m_isToggle;

        //constructor permutations -- params doesn't seem to work for property drawer inputs :( -----------
        public ShowIfDrawer(string name1)
        {
            argValue = new string[] { name1 };
        }
        
        public ShowIfDrawer(string name1, string name2)
        {
            if (name2 == "Toggle")
            {
                m_isToggle = true;
                argValue = new string[] { name1 };
            }
            else
                argValue = new string[] { name1, name2 };
        }

        public ShowIfDrawer(string name1, string name2, string name3)
        {
            argValue = new string[] { name1, name2, name3 };
        }

        public ShowIfDrawer(string name1, string name2, string name3, string name4)
        {
            argValue = new string[] { name1, name2, name3, name4 };
        }

        //-------------------------------------------------------------------------------------------------

        public override void OnGUI (Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
            bElementHidden = false;
            for(int i=0; i<editor.targets.Length; i++)
            {
                //material object that we're targetting...
                Material mat = editor.targets[i] as Material;
                if(mat != null)
                {
                    //check for the dependencies:
                    for (int j = 0; j < argValue.Length; j++)
                    {
                        bElementHidden |= !mat.IsKeywordEnabled(argValue[j]);
                    }
                }
            }

            if (!bElementHidden)
            {
                if (m_isToggle && prop.type == MaterialProperty.PropType.Float)
                    prop.floatValue = EditorGUILayout.Toggle(label, prop.floatValue > 0.0f) ? 1f : 0f;
                else 
                    editor.DefaultShaderProperty(prop, label);
            }
        }

        //We need to override the height so it's not adding any extra (unfortunately texture drawers will still add an extra bit of padding regardless):
        public override float GetPropertyHeight (MaterialProperty prop, string label, MaterialEditor editor)
        {
            //@TODO: manually standardise element compaction
            float height = base.GetPropertyHeight (prop, label, editor);
            return bElementHidden ? 0.0f : height-16;

            return 0;
        }
    }

    public class ShowIfToggleDrawer : MaterialPropertyDrawer
    {
        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
            base.OnGUI(position, prop, label, editor);
        }
    }
}