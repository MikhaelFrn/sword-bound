#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Vérifie que le prefab PlayerCameraRig n'a ni script manquant ni référence manquante.</summary>
public class PrefabIntegrityTests
{
    private const string PrefabPath = "Assets/Prefabs/Camera/PlayerCameraRig.prefab";

    [Test]
    public void Prefab_HasNoMissingScriptsOrReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, "Prefab introuvable : " + PrefabPath);

        int checkedComponents = 0;
        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
        {
            Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject),
                "Script manquant sur " + t.name);

            foreach (Component component in t.GetComponents<Component>())
            {
                Assert.IsNotNull(component, "Composant manquant sur " + t.name);
                checkedComponents++;

                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == null &&
                        property.objectReferenceInstanceIDValue != 0)
                    {
                        Assert.Fail("Référence manquante : " + t.name + "/" + component.GetType().Name + "." + property.propertyPath);
                    }
                }
            }
        }

        Assert.Greater(checkedComponents, 5, "Le prefab semble vide.");
    }

    [Test]
    public void Prefab_HasExpectedHierarchy()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab);

        Transform camera = prefab.transform.Find("Camera");
        Assert.IsNotNull(camera, "Camera manquante");
        Assert.IsNotNull(camera.Find("HandsAnchor"), "HandsAnchor manquant");
        Assert.AreEqual("MainCamera", camera.tag);
        Assert.IsNotNull(camera.GetComponent<Camera>());
        Assert.IsNotNull(camera.GetComponent<AudioListener>());
        Assert.AreEqual(1.6f, prefab.transform.localPosition.y, 0.0001f);
    }
}

#endif
