using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConveyorTrainer.Editor
{
    public static class MvpValidation
    {
        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(MvpBuilder.ScenePath);
            foreach (var text in Object.FindObjectOfType<TrainingPanel>().GetComponentsInChildren<UnityEngine.UI.Text>())
            {
                if (text.name == "Title") text.text = "1/5  Знакомство";
                if (text.name == "Procedure") text.text = "• Включите линию.\n• Изучите конвейер: кнопка i.\n• Изучите датчик: кнопка i.\n• Изучите толкатель: кнопка i.";
                if (text.name == "Feedback") text.text = "Запустите линию и изучите три информационные кнопки.";
                if (text.name == "Equipment") text.text = "Линия: остановлена    Воздух: открыт\nДавление: есть";
            }
            var camera = Object.FindObjectOfType<Camera>();
            Capture(camera, "Logs/WorkshopPreview.png", new Vector3(0.4f, 3.8f, -6.8f), new Vector3(-0.7f, 0.9f, -0.2f));
            Capture(camera, "Logs/PanelPreview.png", new Vector3(2.3f, 1.7f, -3.2f), new Vector3(2.6f, 1.65f, -0.25f));
        }
        public static void BuildAndCapture()
        {
            CapturePreview();
            EditorSceneManager.OpenScene(MvpBuilder.ScenePath);
            MvpBuilder.BuildWindowsPlayer();
        }
        private static void Capture(Camera camera, string path, Vector3 position, Vector3 target)
        {
            camera.transform.position = position; camera.transform.LookAt(target);
            var texture = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(image); Object.DestroyImmediate(texture);
        }
    }
}
