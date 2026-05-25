using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System.Collections.Generic;
using DistantLands.Cozy.Data;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(EclipseModule))]
    public class CozyEclipseModuleEditor : CozyModuleEditor
    {

        EclipseModule module;
        public override ModuleCategory Category => ModuleCategory.atmosphere;
        public override string ModuleTitle => "Eclipse";
        public override string ModuleSubtitle => "Sun Occlusion Module";
        public override string ModuleTooltip => "Manage your weather with more control.";

        public VisualElement GraphContainer => root.Q<VisualElement>("graph-container");
        public Label NextEclipseLabel => root.Q<Label>("next-eclipse-label");
        public Label AngleLabel => root.Q<Label>("angle-label");
        public Label DeclinationLabel => root.Q<Label>("declination-label");
        public VisualElement SelectionContainer => root.Q<VisualElement>("selection-container");
        public VisualElement ProfileContainer => root.Q<VisualElement>("profile-container");


        Button widget;
        VisualElement root;

        void OnEnable()
        {
            if (!target)
                return;

            module = (EclipseModule)target;
        }

        public override Button DisplayWidget()
        {
            widget = SmallWidget();
            Label status = widget.Q<Label>("dynamic-status");
            status.style.fontSize = 8;

            if (module.eclipseRatio > 0.96f)
                status.text = "Total Eclipse";
            else if (module.eclipseRatio > 0.85f)
                status.text = "Partial Eclipse";
            else if (module.eclipseRatio > 0.6f)
                status.text = "Slight Eclipse";
            else
                status.text = "No Eclipse";

            return widget;

        }

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.distantlands.cozy.eclipse/Editor/UI/UXML/eclipse-module-editor.uxml"
            );

            asset.CloneTree(root);

            CozyProfileField<EclipseProfile> eclipseProfile = new CozyProfileField<EclipseProfile>(serializedObject.FindProperty("profile"));
            SelectionContainer.Add(eclipseProfile);

            PropertyField eclipseStyle = new PropertyField();
            eclipseStyle.BindProperty(serializedObject.FindProperty("eclipseStyle"));
            SelectionContainer.Add(eclipseStyle);

            PropertyField eclipseRatio = new PropertyField();
            eclipseRatio.BindProperty(serializedObject.FindProperty("eclipseRatio"));
            SelectionContainer.Add(eclipseRatio);


            eclipseProfile.RegisterCallback((ChangeEvent<CozyProfile> evt) =>
            {
                ProfileContainer.Clear();
                InspectorElement inspector = new InspectorElement(module.profile);
                inspector.AddToClassList("p-0");
                ProfileContainer.Add(inspector);
            });

            InspectorElement inspector = new InspectorElement(module.profile);
            inspector.AddToClassList("p-0");
            ProfileContainer.Add(inspector);

            root.RegisterCallback<PointerMoveEvent>(evt =>
            {
                RenderGraph();
            });

            RenderGraph();

            return root;

        }



        public void RenderGraph()
        {

            module.GetAngles(out float orbitAngle, out float declinationAngle);

            module.GetNextEclipseDate(out int day, out int year);
            string dayPlural = day == 1 ? "day" : "days";
            string yearPlural = year == 1 ? "year" : "years";

            NextEclipseLabel.text = $"Next eclipse in {day} {dayPlural} and {year} {yearPlural}";
            AngleLabel.text = $"Angle Difference: {Mathf.Round(200 * orbitAngle)}%";
            DeclinationLabel.text = $"Declination Difference: {Mathf.Round(200 * declinationAngle)}%";

            GraphContainer.Clear();

            VisualElement graphHolder = new VisualElement();
            graphHolder.AddToClassList("graph-section");

            graphHolder.generateVisualContent += (MeshGenerationContext context) =>
            {
                float width = graphHolder.contentRect.width;
                float height = graphHolder.contentRect.height;


                var painter = context.painter2D;

                painter.lineWidth = 2;
                painter.strokeColor = Branding.lightGreyAccent;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, height));
                painter.LineTo(new Vector2(width, height));
                painter.MoveTo(new Vector2(0, height));
                painter.LineTo(new Vector2(0, 0));
                painter.MoveTo(new Vector2(0, height / 2f));
                painter.LineTo(new Vector2(width, height / 2f));
                painter.MoveTo(new Vector2(width, height));
                painter.LineTo(new Vector2(width, 0));
                painter.MoveTo(new Vector2(width / 2f, height));
                painter.LineTo(new Vector2(width / 2f, 0));
                painter.MoveTo(new Vector2(0, 0));
                painter.LineTo(new Vector2(width, 0));
                painter.Stroke();

                Vector2 moonPosition = new Vector2(width * RemapFloat(module.OrbitCyclePosition()), height * SineWaveTransform(module.DeclinationCyclePosition()));
                Vector2 sunPosition = new Vector2(width / 2f, height / 2f);

                painter.strokeColor = Branding.white;

                painter.BeginPath();
                painter.Arc(sunPosition, 15, 0, 360, ArcDirection.Clockwise);
                painter.Stroke();



                painter.strokeColor = Branding.orange;
                float iterations = 16f;
                int iterativeWidth = 1;

                for (int i = 1; i <= iterations; i++)
                {
                    float strength = 0.5f - (i / iterations * 0.5f);
                    painter.strokeColor = new Color(Branding.orange.r, Branding.orange.g, Branding.orange.b, strength);
                    painter.BeginPath();
                    painter.Arc(
                        new Vector2(width * RemapFloat(module.OrbitCyclePosition(i * iterativeWidth)), height * SineWaveTransform(module.DeclinationCyclePosition(i))),
                        9, 0, 360,
                        ArcDirection.Clockwise);
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(
                        new Vector2(width * RemapFloat(module.OrbitCyclePosition(-i * iterativeWidth)), height * SineWaveTransform(module.DeclinationCyclePosition(-i))),
                        9, 0, 360,
                        ArcDirection.Clockwise);
                    painter.Stroke();
                }

                painter.strokeColor = Branding.orange;
                painter.BeginPath();
                painter.Arc(moonPosition
                    ,
                    13, 0, 360,
                    ArcDirection.Clockwise);
                painter.Stroke();

                float declinationCyclePosition = module.DeclinationCyclePosition();
                float orbitCyclePosition = module.OrbitCyclePosition();



            };

            GraphContainer.Add(graphHolder);

        }


        float RemapFloat(float input)
        {
            float remainder = input;
            remainder += 0.5f;
            remainder %= 1;
            remainder = Mathf.Abs(remainder);
            return remainder;
        }
        float SineWaveTransform(float input)
        {
            return 0.5f + 0.5f * (float)Mathf.Sin(2f * Mathf.PI * (input - 0.5f));
        }


        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/eclipse-module");
        }


    }
}