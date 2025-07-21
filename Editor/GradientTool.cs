using UnityEditor;
using UnityEngine;

using System.IO;
using Unity.Mathematics;

namespace FS.Rendering.Editor
{
    [CreateAssetMenu(fileName = "Gradient", menuName = "Gradient")]
    public class GradientTool : ScriptableObject
    {
        public enum PowerOfTwoResolution
        {
            // What even is this? 2, 3, 4, 5?
            // May as well make it a slider...

            _2 = 2,
            _3 = 3,
            _4 = 4,
            _5 = 5,
            _7 = 7,

            // MY HAPPY PLACE.
            // MY HAPPY PLACE.

            _8 = 8,
            _16 = 16,
            _32 = 32,
            _64 = 64,
            _128 = 128,
            _256 = 256,
            _512 = 512,
            _1024 = 1024
        }

        // These are static so they are global UI settings for this type of inspector.

        public static bool showLabSection;
        public static bool showPostProcessingSection;

        [Header("Settings")]

        [Space]

        public Gradient gradient;

        [HideInInspector]
        public Gradient gradientHSV;

        Texture2D texture;

        public PowerOfTwoResolution resolution = PowerOfTwoResolution._256;

        public int width => (int)resolution;
        public const int height = 1;

        [Space]

        [Tooltip("Keep this disabled when not actively using/previewing: automatically generates texture when properties change. When disabled, you must manually click the 'Generate/Update Texture' button.")]
        public bool autoSave;

        // Skipped in actual drawing: used as a separator indicator for custom inspector.

        public int CUSTOM_HEADER_COLOUR_LAB;

        //[Header("Colour Lab")]

        //[Space]

        [Range(-180.0f, 180.0f)]
        public float hueOffset;

        // Unclamped variants may result in changes to the underlying hues.

        [Space]

        [Range(-1.0f, 1.0f)]
        public float saturationOffset;

        [Range(-1.0f, 1.0f)]
        public float saturationOffsetUnclamped;

        [Space]

        [Range(-1.0f, 1.0f)]
        public float valueOffset;

        [Range(-1.0f, 1.0f)]
        public float valueOffsetUnclamped;

        [Space]

        [Range(0.0f, 2.0f)]
        public float saturationScale = 1.0f;

        [Space]

        [ColorUsage(false, false)]
        public Color multiplyColour = Color.white;

        Color[] _colours;
        public Color[] colours => GetColours();

        // ...

        public int CUSTOM_HEADER_POST_PROCESSING;

        // Post-processing.

        public Gradient multiplyGradient = new();

        [Space]

        public bool smoothstep;

        [Space]

        [Range(0.0f, 1.0f)]
        public float smoothstepMin = 0.0f;
        [Range(0.0f, 1.0f)]
        public float smoothstepMax = 1.0f;

        [Space]

        public float power = 1.0f;

        void OnEnable()
        {
            // Turn this off on load.
            // It should never remain on.

            autoSave = false;

            if (texture)
            {
                Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(this));

                if (!GetTextureFromSubAssets(subAssets))
                {
                    AddTextureAsSubAsset();
                }
            }
        }

        void OnValidate()
        {
            // These are sometimes null.
            // Need to initialize in the editor.

            //gradient ??= new Gradient();
            //multiplyGradient ??= new Gradient();

            // ...

            if (!autoSave)
            {
                return;
            }

            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            UpdateTexture();
        }

        public Texture2D GetTexture()
        {
            bool textureCreated = EnsureTextureExists();

            if (textureCreated)
            {
                UpdateTexture();
            }

            return texture;
        }
        public Color[] GetColours()
        {
            // Re/initialize texture colour buffer if needed.

            if (_colours == null || _colours.Length != width * height)
            {
                if (_colours != null)
                {
                    Debug.LogWarning("Re-initializing colours array due to size change.");
                }

                _colours = new Color[width * height];
            }

            return _colours;
        }

        public void SetTexture(Texture2D newTexture)
        {
            texture = newTexture;
        }

        // Returns true if texture was created, false if it already exists.

        bool EnsureTextureExists()
        {
            // If no texture, try to find it in sub-assets.

            if (!texture)
            {
                Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(this));
                texture = GetTextureFromSubAssets(subAssets);
            }

            // If STILL no texture, create it.

            if (!texture)
            {
                CreateTextureSubAsset();
                AddTextureAsSubAsset();

                return true;
            }

            return false;
        }

        // Updates colour buffer with final output colours/values.

        public void UpdateColourBuffer()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t = x / (width - 1.0f);

                    ref Color colour = ref colours[x + (y * width)];

                    // Mathf.SmoothStep is not the same as a shader smoothstep.
                    // For shader-like remapping smoothstep, we use math.smoothstep.

                    if (smoothstep)
                    {
                        t = math.smoothstep(smoothstepMin, smoothstepMax, t);
                    }

                    t = Mathf.Pow(t, power);

                    colour = gradient.Evaluate(t);
                    colour *= multiplyGradient.Evaluate(t);
                }
            }
        }

        public void UpdateTexture()
        {
            EnsureTextureExists();
            UpdateColourBuffer();

            if (GetTexture().width != width || GetTexture().height != height)
            {
                GetTexture().Reinitialize(width, height);
            }

            GetTexture().SetPixels(colours);
            GetTexture().Apply();

            AssetDatabase.SaveAssets();
        }

        Texture2D GetTextureFromSubAssets(Object[] objects)
        {
            foreach (Object obj in objects)
            {
                if (obj is Texture2D tex)
                {
                    return tex;
                }
            }

            return null;
        }

        void AddTextureAsSubAsset()
        {
            bool fileCreated = EditorUtility.IsPersistent(this);

            if (fileCreated)
            {
                AssetDatabase.AddObjectToAsset(texture, this);
                AssetDatabase.SaveAssets();
            }
        }

        void CreateTextureSubAsset()
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Texture",
                wrapMode = TextureWrapMode.Clamp
            };
        }

        public void ExportTexture()
        {
            EnsureTextureExists();

            string assetPath = AssetDatabase.GetAssetPath(this);
            string directory = Path.GetDirectoryName(assetPath);
            string filePath = Path.Combine(directory, name + ".png");

            byte[] data = GetTexture().EncodeToPNG();

            File.WriteAllBytes(filePath, data);

            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(filePath);

            importer.wrapMode = texture.wrapMode;
            importer.filterMode = texture.filterMode;

            importer.textureCompression = TextureImporterCompression.Uncompressed;

            AssetDatabase.Refresh();

            Debug.Log("Texture exported to: " + filePath);
        }

        public void DeleteTextureSubAsset()
        {
            if (texture)
            {
                AssetDatabase.RemoveObjectFromAsset(texture);

                DestroyImmediate(texture, true);
                texture = null;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.SetDirty(this);
            }
            else
            {
                Debug.LogWarning("No texture to delete.");
            }
        }

        public void ResetLab()
        {
            hueOffset = 0.0f;

            saturationOffset = 0.0f;
            saturationOffsetUnclamped = 0.0f;

            valueOffset = 0.0f;
            valueOffsetUnclamped = 0.0f;

            saturationScale = 1.0f;

            multiplyColour = Color.white;
        }
        public void ResetPostProcessing()
        {
            multiplyGradient = new Gradient();

            smoothstep = false;

            smoothstepMin = 0.0f;
            smoothstepMax = 1.0f;

            power = 1.0f;
        }
    }

    [CustomEditor(typeof(GradientTool))]
    public class GradientToolEditor : UnityEditor.Editor
    {
        Texture2D labPreviewTexture;
        Texture2D postProcessingPreviewTexture;

        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnEditorUpdate()
        {
            GradientTool generator = (GradientTool)target;

            if (Selection.activeObject != target)
            {
                generator.autoSave = false; // Disable auto-save when not selected.
                //Debug.Log($"User navigated away from the inspector of {target.name}.");

                EditorApplication.update -= OnEditorUpdate;
            }
        }

        // Default gradient has two white keys at time = 0.0 and 1.0.

        bool IsDefaultGradient(Gradient gradient)
        {
            if (gradient == null)
            {
                return true;
            }

            GradientAlphaKey[] alphaKeys = gradient.alphaKeys;
            GradientColorKey[] colourKeys = gradient.colorKeys;

            return

                alphaKeys.Length == 2 &&

                (alphaKeys[0].time == 0.0f && alphaKeys[0].alpha == 1.0f) &&
                (alphaKeys[1].time == 1.0f && alphaKeys[1].alpha == 1.0f) &&

                colourKeys.Length == 2 &&

                (colourKeys[0].time == 0.0f && colourKeys[0].color == Color.white) &&
                (colourKeys[1].time == 1.0f && colourKeys[1].color == Color.white);
        }

        // Draw custom editor inspector.

        public override void OnInspectorGUI()
        {
            // ...

            serializedObject.Update();

            // ...

            GradientTool generator = (GradientTool)target;

            generator.gradient ??= new Gradient();
            generator.gradientHSV ??= new Gradient();

            // Re-acquire texture from sub-assets for preview.

            if (!generator.GetTexture())
            {
                Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(generator));

                foreach (Object obj in subAssets)
                {
                    if (obj is Texture2D foundTexture)
                    {
                        generator.SetTexture(foundTexture); break;
                    }
                }
            }

            // ...

            const int SMALL_BUTTON_WIDTH = 128 + 32;

            GUIStyle foldoutHeaderDefaultStyle = new(EditorStyles.foldoutHeader);
            GUIStyle foldoutHeaderHighlightStyle = new(foldoutHeaderDefaultStyle);

            Color highlightColour = Color.yellow;

            foldoutHeaderHighlightStyle.normal.textColor = highlightColour;
            foldoutHeaderHighlightStyle.onNormal.textColor = highlightColour;

            foldoutHeaderHighlightStyle.active.textColor = highlightColour;
            foldoutHeaderHighlightStyle.onActive.textColor = highlightColour;

            foldoutHeaderHighlightStyle.hover.textColor = highlightColour;
            foldoutHeaderHighlightStyle.onHover.textColor = highlightColour;

            foldoutHeaderHighlightStyle.focused.textColor = highlightColour;
            foldoutHeaderHighlightStyle.onFocused.textColor = highlightColour;

            GUIStyle previewLabelStyle = new(EditorStyles.label)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,

                fontSize = 12,
            };

            // Draw colour lab settings.

            {
                SerializedProperty property = serializedObject.GetIterator();
                bool passedCustomHeader = false;

                bool labChanged =

                    generator.hueOffset != 0.0f ||

                    generator.saturationOffset != 0.0f ||
                    generator.saturationOffsetUnclamped != 0.0f ||

                    generator.valueOffset != 0.0f ||
                    generator.valueOffsetUnclamped != 0.0f ||

                    generator.saturationScale != 1.0f ||

                    generator.multiplyColour != Color.white;

                if (property.NextVisible(true))
                {
                    do
                    {
                        if (property.name == nameof(generator.CUSTOM_HEADER_COLOUR_LAB))
                        {
                            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                            // If lab properties have changed, highlight the header in yellow.

                            GradientTool.showLabSection = EditorGUILayout.Foldout(

                                GradientTool.showLabSection, "Colour Lab", true,
                                !labChanged ? foldoutHeaderDefaultStyle : foldoutHeaderHighlightStyle);

                            passedCustomHeader = true;
                        }
                        else
                        {
                            if (!passedCustomHeader)
                            {
                                EditorGUILayout.PropertyField(property, true);
                            }
                            else if (GradientTool.showLabSection)
                            {
                                EditorGUILayout.PropertyField(property, true);
                            }
                        }
                    }
                    while (property.NextVisible(false) && property.name != nameof(generator.CUSTOM_HEADER_POST_PROCESSING));
                }

                if (GradientTool.showLabSection)
                {
                    EditorGUILayout.Space();

                    GUI.enabled = labChanged;

                    {
                        // Draw small button to reset lab properties.

                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("Reset Lab", GUILayout.MaxWidth(SMALL_BUTTON_WIDTH)))
                        {
                            Undo.RecordObject(generator, "Reset Lab");

                            generator.ResetLab();

                            EditorUtility.SetDirty(generator);
                        }

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Space();

                        GenerateLabPreview(generator);

                        Rect labPreviewRect = GUILayoutUtility.GetRect(

                            EditorGUIUtility.currentViewWidth - 40, 32,
                            GUILayout.ExpandWidth(true));

                        EditorGUI.DrawPreviewTexture(labPreviewRect, labPreviewTexture);

                        // Draw label normally.

                        GUI.enabled = true;
                        EditorGUI.DropShadowLabel(labPreviewRect, $"Lab Preview", previewLabelStyle);
                        GUI.enabled = labChanged;

                        EditorGUILayout.Space();

                        if (GUILayout.Button("Apply Lab to Gradient"))
                        {
                            Undo.RecordObject(generator, "Apply Lab to Gradient");

                            //generator.ResetOffsets();

                            GradientColorKey[] colorKeys = generator.gradientHSV.colorKeys;

                            generator.gradient.colorKeys = colorKeys;

                            EditorUtility.SetDirty(generator);

                            if (generator.autoSave)
                            {
                                generator.UpdateTexture();
                            }
                        }

                        //EditorGUILayout.Space();
                    }

                    GUI.enabled = true;

                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                }
            }

            // Post-processing section.

            bool postProcessingChanged = !IsDefaultGradient(generator.multiplyGradient);

            GradientTool.showPostProcessingSection = EditorGUILayout.Foldout(

                GradientTool.showPostProcessingSection, "Post-Processing", true,
                !postProcessingChanged ? foldoutHeaderDefaultStyle : foldoutHeaderHighlightStyle);

            if (GradientTool.showPostProcessingSection)
            {
                //EditorGUILayout.LabelField("Post-Processing", EditorStyles.boldLabel);

                // Multiply gradient.

                EditorGUILayout.Space();

                //SerializedProperty multiplyGradientProperty = serializedObject.FindProperty(nameof(generator.multiplyGradient));
                //EditorGUILayout.PropertyField(multiplyGradientProperty);

                //EditorGUILayout.GradientField(ObjectNames.NicifyVariableName(nameof(generator.multiplyGradient)), generator.multiplyGradient);

                //EditorGUILayout.Space();

                //SerializedProperty powerProperty = serializedObject.FindProperty(nameof(generator.power));
                //EditorGUILayout.PropertyField(powerProperty);

                // Draw post-processing properties block.

                SerializedProperty property = serializedObject.FindProperty(nameof(generator.CUSTOM_HEADER_POST_PROCESSING));

                if (property.NextVisible(true))
                {
                    do
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                    while (property.NextVisible(false));
                }

                EditorGUILayout.Space();

                GUI.enabled = postProcessingChanged;

                // Draw small button to reset post-processing.

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Reset Post-Processing", GUILayout.MaxWidth(SMALL_BUTTON_WIDTH)))
                {
                    Undo.RecordObject(generator, "Reset Post-Processing");

                    generator.ResetPostProcessing();

                    EditorUtility.SetDirty(generator);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUI.enabled = true;

            // Renders exactly what the outcome texture will look like.

            EditorGUILayout.LabelField("Texture Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GeneratePostProcessingPreview(generator);

            Rect postProcessingPreviewRect = GUILayoutUtility.GetRect(

                EditorGUIUtility.currentViewWidth - 40, 32,
                GUILayout.ExpandWidth(true));

            EditorGUI.DrawPreviewTexture(postProcessingPreviewRect, postProcessingPreviewTexture);

            EditorGUI.DropShadowLabel(postProcessingPreviewRect, $"Render Preview: {generator.width} x {GradientTool.height}", previewLabelStyle);

            EditorGUILayout.Space();
            //EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Texture asset section.

            {
                // Draw baked texture preview after Apply HSV button.

                EditorGUILayout.LabelField("Texture Asset/File", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                Rect texturePreviewRect = GUILayoutUtility.GetRect(

                    EditorGUIUtility.currentViewWidth - 40, 32,
                    GUILayout.ExpandWidth(true)
                );

                //float textureWidth = generator.GetTexture().width;
                //float texturePercentage = (textureWidth / generator.width) * 100.0f;

                EditorGUI.DrawPreviewTexture(texturePreviewRect, generator.GetTexture());

                EditorGUI.DropShadowLabel(texturePreviewRect, $"Current Texture: {generator.GetTexture().width} x {GradientTool.height}", previewLabelStyle);
                //EditorGUI.DropShadowLabel(texturePreviewRect, $"Current Texture: {generator.GetTexture().width} x {GradientTool.height} ({texturePercentage:0.##}%)", previewLabelStyle);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                if (GUILayout.Button("Generate/Update Texture"))
                {
                    generator.UpdateTexture();
                }
                if (GUILayout.Button("Export Texture"))
                {
                    generator.UpdateTexture();
                    generator.ExportTexture();
                }

                //EditorGUILayout.Space();

                //if (GUILayout.Button("Delete Texture"))
                //{
                //    generator.DeleteTextureSubAsset();
                //}
            }

            // ...

            serializedObject.ApplyModifiedProperties();
        }

        // ...

        void GenerateLabPreview(GradientTool generator)
        {
            if (!labPreviewTexture || labPreviewTexture.width != generator.width)
            {
                if (labPreviewTexture)
                {
                    DestroyImmediate(labPreviewTexture, true);
                }

                labPreviewTexture = new Texture2D(generator.width, GradientTool.height, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            GradientColorKey[] originalKeys = generator.gradient.colorKeys;
            GradientColorKey[] labKeys = new GradientColorKey[originalKeys.Length];

            for (int i = 0; i < originalKeys.Length; i++)
            {
                Color colour = originalKeys[i].color;
                Color.RGBToHSV(colour, out float h, out float s, out float v);

                h = Mathf.Repeat(h + (generator.hueOffset / 360.0f), 1.0f);
                s += generator.saturationOffset;

                v += generator.valueOffset;

                s = Mathf.Clamp01(s);
                v = Mathf.Clamp01(v);

                s += generator.saturationOffsetUnclamped;
                v += generator.valueOffsetUnclamped;

                colour = Color.HSVToRGB(h, s, v, false);
                colour.a = originalKeys[i].color.a;

                // Apply saturation scale.

                Vector3 rgb = new(colour.r, colour.g, colour.b);
                float intensity = Vector3.Dot(rgb, new Vector3(0.2126f, 0.7152f, 0.0722f));

                rgb = Vector3.Lerp(new Vector3(intensity, intensity, intensity), rgb, generator.saturationScale);

                colour = new Color(rgb.x, rgb.y, rgb.z, colour.a);
                colour *= generator.multiplyColour;

                labKeys[i] = new GradientColorKey(colour, originalKeys[i].time);
            }

            generator.gradientHSV.colorKeys = labKeys;

            // Calculate colours.

            Color[] colours = generator.colours;

            for (int y = 0; y < GradientTool.height; y++)
            {
                for (int x = 0; x < generator.width; x++)
                {
                    float t = x / (generator.width - 1.0f);

                    ref Color colour = ref colours[x + (y * generator.width)];

                    colour = generator.gradientHSV.Evaluate(t);
                }
            }

            // Apply to texture.

            labPreviewTexture.SetPixels(colours);
            labPreviewTexture.Apply();
        }

        // ...

        void GeneratePostProcessingPreview(GradientTool generator)
        {
            // Make sure preview texture is ready.

            if (!postProcessingPreviewTexture || postProcessingPreviewTexture.width != generator.width)
            {
                if (postProcessingPreviewTexture)
                {
                    DestroyImmediate(postProcessingPreviewTexture, true);
                }

                postProcessingPreviewTexture = new Texture2D(generator.width, GradientTool.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,

                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            // Calculate colours.

            generator.UpdateColourBuffer();

            // Apply to texture.

            postProcessingPreviewTexture.SetPixels(generator.colours);
            postProcessingPreviewTexture.Apply();
        }
    }
}