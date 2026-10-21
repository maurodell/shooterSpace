using System.Collections.Generic;
using UnityEngine;

namespace NavesShooter.Core
{
    // Opcional: componentes que necesitan enterarse de que salen del pool o vuelven a él. El reseteo de estado
    // (vida, velocidad, timers) va en OnTakenFromPool, no en Awake/Start, que corren una sola vez por instancia.
    public interface IPoolable
    {
        void OnTakenFromPool();
        void OnReturnedToPool();
    }

    // Pool de instancias de un prefab. Precrea `initialSize` instancias inactivas y crece instanciando más cuando se
    // agota; las devueltas quedan inactivas bajo `container` hasta el próximo Get. Nunca destruye instancias.
    public class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform container;
        private readonly Stack<T> inactive;

        public int CountAll { get; private set; }
        public int CountInactive => inactive.Count;
        public int CountActive => CountAll - inactive.Count;

        // `container` es el padre de las instancias; si es null se crea un GameObject vacío en la raíz de la escena.
        public ObjectPool(T prefab, int initialSize, Transform container = null)
        {
            this.prefab = prefab;
            this.container = container != null ? container : new GameObject($"Pool ({prefab.name})").transform;
            inactive = new Stack<T>(Mathf.Max(initialSize, 1));

            for (int i = 0; i < initialSize; i++) inactive.Push(Create());
        }

        // Ubica la instancia antes de activarla, para que OnEnable y el Rigidbody2D arranquen ya en su lugar.
        public T Get(Vector3 position, Quaternion rotation)
        {
            T item = inactive.Count > 0 ? inactive.Pop() : Create();
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            if (item is IPoolable poolable) poolable.OnTakenFromPool();
            return item;
        }

        // Ignora instancias ya inactivas, así devolver dos veces en el mismo frame (p. ej. choque y fin de vida) es inocuo.
        public void Release(T item)
        {
            if (item == null || !item.gameObject.activeSelf) return;
            if (item is IPoolable poolable) poolable.OnReturnedToPool();
            item.gameObject.SetActive(false);
            inactive.Push(item);
        }

        // Devuelve la instancia inactiva. Su Awake corre acá, una sola vez.
        private T Create()
        {
            T item = Object.Instantiate(prefab, container);
            item.gameObject.SetActive(false);
            item.name = $"{prefab.name} {CountAll}";
            CountAll++;
            return item;
        }
    }
}
