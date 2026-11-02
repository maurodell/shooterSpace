using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace NavesShooter.Core
{
    // Munición reutilizable. Quien dispara llama `TryConsume` antes de cada proyectil y no dispara si devuelve false.
    // Valores en un AmmoConfig (leído en vivo). La regeneración pasiva suma `regenAmount` cada `regenInterval` segundos
    // de tiempo escalado (en pausa no corre) solo mientras la munición esté por debajo de `RegenCap`; nunca lo pasa. Cada
    // llamada a `TryConsume` (con o sin munición) la frena `regenDelayAfterFire` segundos: manteniendo el disparo en 0 no
    // sale ningún tiro, y al soltarlo se recupera.
    // Quien la usa resetea con `Restore` al reaparecer. En el Editor, con `debugKeys`, R recarga completo y T vacía.
    public class Ammo : MonoBehaviour
    {
        [SerializeField] private AmmoConfig config;
        [SerializeField, Tooltip("Solo Editor: R recarga completo, T vacía la munición.")] private bool debugKeys = true;

        // (actual, máximo) ante cualquier cambio de `Current`.
        public event Action<int, int> OnAmmoChanged;
        // Al llegar a 0.
        public event Action OnAmmoEmpty;
        // Cantidad que realmente se sumó (recarga, regeneración o `Restore`).
        public event Action<int> OnAmmoRefilled;

        private float regenTimer;
        private float regenBlockedUntil;

        public int Max => config.maxAmmo;
        public int Current { get; private set; }
        public int CostPerShot => config.costPerShot;
        public bool IsEmpty => Current <= 0;
        public bool IsFull => Current >= Max;
        public float Normalized => Max > 0 ? (float)Current / Max : 0f;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError($"{nameof(Ammo)}: falta asignar el AmmoConfig.", this);
                enabled = false;
                return;
            }
            Current = Mathf.Min(config.initialAmmo, config.maxAmmo);
        }

        // Devuelve si había suficiente munición; si no, no gasta nada.
        public bool TryConsume(int amount)
        {
            if (!enabled || amount < 0) return false;
            if (amount == 0) return true;
            regenBlockedUntil = Time.time + config.regenDelayAfterFire;
            if (Current < amount) return false;

            Set(Current - amount);
            return true;
        }

        // Suma con tope en el máximo. Devuelve lo que realmente se sumó.
        public int Add(int amount)
        {
            if (!enabled || amount <= 0) return 0;
            int added = Mathf.Min(amount, Max - Current);
            if (added <= 0) return 0;

            Set(Current + added);
            OnAmmoRefilled?.Invoke(added);
            return added;
        }

        public void Refill() => Add(Max - Current);

        public void Clear()
        {
            if (enabled && Current > 0) Set(0);
        }

        // Munición inicial y regeneración desde cero (al reaparecer o pasar de nivel).
        public void Restore()
        {
            if (!enabled) return;
            regenTimer = 0f;
            regenBlockedUntil = 0f;
            int initial = Mathf.Min(config.initialAmmo, Max);
            if (initial > Current) Add(initial - Current);
            else if (initial < Current) Set(initial);
        }

        private void Set(int value)
        {
            bool wasEmpty = IsEmpty;
            Current = value;
            OnAmmoChanged?.Invoke(Current, Max);
            if (!wasEmpty && IsEmpty) OnAmmoEmpty?.Invoke();
        }

        private void Update()
        {
            // Si se bajó el máximo en vivo desde el asset.
            if (Current > Max) Set(Max);

            if (config.regenEnabled && Current < config.RegenCap && Time.time >= regenBlockedUntil)
            {
                regenTimer += Time.deltaTime;
                if (regenTimer >= config.regenInterval)
                {
                    regenTimer -= config.regenInterval;
                    Add(Mathf.Min(config.regenAmount, config.RegenCap - Current));
                }
            }
            // Arriba del tope o disparando no acumula: el primer tick llega un intervalo completo después.
            else regenTimer = 0f;

#if UNITY_EDITOR
            Keyboard keyboard = Keyboard.current;
            if (debugKeys && keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) Refill();
                if (keyboard.tKey.wasPressedThisFrame) Clear();
            }
#endif
        }
    }
}
