using UnityEngine;

namespace CraftPlanner.Data
{
    /// <summary>
    /// Определение ресурса. Базовый или производимый.
    /// ID используется как технический идентификатор.
    /// </summary>
    [CreateAssetMenu(fileName = "NewResource", menuName = "Craft/Resource")]
    public class ResourceSO : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private bool _isBase;

        public string Id => _id;
        public string DisplayName => _displayName;
        public bool IsBase => _isBase;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_id))
                _id = name;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceSO other && other.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }
    }
}