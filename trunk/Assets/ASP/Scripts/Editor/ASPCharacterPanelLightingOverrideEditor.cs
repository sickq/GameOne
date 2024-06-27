using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ASP.Scripts.Editor
{
    partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {
        partial void DrawLightingDirectionOverrideProperty(VisualElement root, ASPCharacterPanel characterPanel,
            SerializedObject serializedObject)
        {
            var container = new VisualElement();
            container.style.marginTop = 5;
            container.style.justifyContent = Justify.FlexStart;
            container.style.alignItems = Align.FlexStart;
            container.style.flexDirection = FlexDirection.Row;

            var lightDirectionOverrideModeProperty = serializedObject.FindProperty("m_overrideMode");

            var currentLightOverrideMethodLabel =
                new Label("Current Light Direction Override Method : " + (ASPCharacterPanel.OverrideMode)lightDirectionOverrideModeProperty.enumValueIndex);
            currentLightOverrideMethodLabel.name = "LightOverrideMethodLabel";
            currentLightOverrideMethodLabel.style.marginTop = 5;
            root.Add(currentLightOverrideMethodLabel);
        
            var lightDirectionOverrideField =
                new EnumField((ASPCharacterPanel.OverrideMode)lightDirectionOverrideModeProperty.enumValueIndex);
            lightDirectionOverrideField.style.width = new StyleLength(new Length( 80, LengthUnit.Percent));
            lightDirectionOverrideField.style.marginTop = 5;
            var applyMethodButton = new Button();
            applyMethodButton.text = "Apply";
            applyMethodButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.8f));
            applyMethodButton.style.width= new StyleLength(new Length( 20, LengthUnit.Percent));
            applyMethodButton.RegisterCallback<ClickEvent>(e =>
            {
                lightDirectionOverrideModeProperty.enumValueIndex =
                    (int)(ASPCharacterPanel.OverrideMode)lightDirectionOverrideField.value;

                serializedObject.ApplyModifiedProperties();
                root.Q<Label>("LightOverrideMethodLabel").text = "Current Light Direction Override Method : " +
                                                                 (ASPCharacterPanel.OverrideMode)lightDirectionOverrideModeProperty.enumValueIndex;
                characterPanel.UpdateLightDirectionOverrideParam();
            });

            container.Add(lightDirectionOverrideField);
            container.Add(applyMethodButton);
            root.Add(container);
        
            var lightDirectionEulerProperty = serializedObject.FindProperty("m_overrideLightAngle");
            var lightDirectionEulerPicker = new Vector3Field("Override Light Euler Angle");
            lightDirectionEulerPicker.style.marginTop = 10;
            lightDirectionEulerPicker.value = lightDirectionEulerProperty.vector3Value;
            lightDirectionEulerPicker.RegisterCallback<ChangeEvent<Vector3>>(e =>
            {
                lightDirectionEulerProperty.vector3Value = e.newValue;
                serializedObject.ApplyModifiedProperties();
                characterPanel.UpdateLightDirectionOverrideParam();
            });
            root.Add(lightDirectionEulerPicker);
            
            var headBoneTransformProperty = serializedObject.FindProperty("HeadBoneTransform");
            var headBonePropertyField = new PropertyField(headBoneTransformProperty);
            headBonePropertyField.tooltip =
                "Bind Head bone for accurate face shadow map calculation, otherwise the face shadow using character's object direction.";
            headBonePropertyField.style.marginTop = 10;
            headBonePropertyField.RegisterCallback<ChangeEvent<Transform>>(e =>
            {
                serializedObject.ApplyModifiedProperties();
                characterPanel.UpdateLightDirectionOverrideParam();
            });
            root.Add(headBonePropertyField);
        }
    }
}