using System.Text;
using NavesShooter.Asteroids;
using NavesShooter.Core;
using NavesShooter.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NavesShooter.UI
{
    // Overlay de depuración de la dificultad (IMGUI): progreso, presupuesto de amenaza usado/máximo, enemigos y cometas
    // activos contra su máximo, estado de la oleada y los multiplicadores actuales. Se muestra/oculta con `toggleKey`.
    // Solo existe en el Editor y en builds de desarrollo (variante de código DEBUG).
    public class DifficultyDebugOverlay : MonoBehaviour
    {
        // Los campos existen en todas las builds: la serialización de la escena no puede cambiar entre Editor y player.
        [SerializeField] private DifficultyDirector director;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private LargeCometField largeCometField;
        [SerializeField] private Key toggleKey = Key.F3;
        [SerializeField] private bool visible;

#if UNITY_EDITOR || DEBUG
        private readonly StringBuilder text = new StringBuilder(512);
        private GUIStyle style;

        // Sin GUILayout (solo GUI.Box): evita las allocations del pase de layout de IMGUI en cada frame.
        private void Awake() => useGUILayout = false;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || director == null) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
                style.normal.textColor = Color.white;
            }
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.016f));
            style.padding = new RectOffset(style.fontSize / 2, style.fontSize / 2, style.fontSize / 2, style.fontSize / 2);

            text.Clear();
            text.Append("<b>DIFICULTAD</b>  (").Append(toggleKey).Append(")\n");
            text.Append("Progreso: ").Append(Mathf.RoundToInt(director.Progress * 100f)).Append("%  vuelta ").Append(director.Cycle).Append('\n');

            int used = director.UsedBudget;
            int reserved = director.ReservedCount(ThreatKind.Enemy) * director.Cost(ThreatKind.Enemy);
            string budgetColor = used + reserved >= director.Budget ? "#ff8040" : "#80ff80";
            text.Append("Presupuesto: <color=").Append(budgetColor).Append('>').Append(used).Append(" / ")
                .Append(director.Budget).Append("</color>  (reservado ").Append(reserved).Append(")\n");

            text.Append("Enemigos: ").Append(director.ActiveCount(ThreatKind.Enemy)).Append(" / ").Append(director.MaxEnemies);
            if (enemySpawner != null)
            {
                if (enemySpawner.InWave)
                    text.Append("  oleada de ").Append(enemySpawner.WaveSize).Append(" (faltan ").Append(enemySpawner.WaveRemaining).Append(')');
                else
                    text.Append("  calma ").Append(enemySpawner.CalmRemaining.ToString("0.0")).Append(" s");
            }
            text.Append('\n');

            text.Append("  intervalo ").Append(director.EnemySpawnInterval.ToString("0.0")).Append(" s, respiro ")
                .Append(director.WaveBreak.ToString("0.0")).Append(" s\n");
            text.Append("  cadencia x").Append(director.EnemyFireRate.ToString("0.00")).Append(", desvío x")
                .Append(director.EnemyInaccuracy.ToString("0.00")).Append('\n');

            text.Append("Cometas chicos: ").Append(director.ActiveCount(ThreatKind.SmallComet)).Append(" / ")
                .Append(director.SmallCometCount).Append("  vel x").Append(director.SmallCometSpeed.ToString("0.00")).Append('\n');

            if (largeCometField != null)
            {
                text.Append("Cometas grandes: ").Append(largeCometField.ActiveCount).Append(" activos / ")
                    .Append(largeCometField.Count).Append("  densidad ")
                    .Append(director.LargeCometDensityAt(director.Progress).ToString("0.00"));
            }

            var content = new GUIContent(text.ToString());
            Vector2 size = style.CalcSize(content);
            GUI.Box(new Rect(10f, Screen.height * 0.2f, size.x, size.y), content, style);
        }
#endif
    }
}
