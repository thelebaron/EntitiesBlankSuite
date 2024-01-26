using Unity.Entities;
using UnityEngine;

namespace Junk.Contacts.Hybrid
{
    public class ContactsAuthoring : MonoBehaviour
    {
        [Header("Decals")]
        public GameObject BulletHoleTiny;

        public class ContactsAuthoringBaker : Baker<ContactsAuthoring>
        {
            public override void Bake(ContactsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,
                    new PrefabEntityData
                    {
                        BulletHoleTiny = GetEntity(authoring.BulletHoleTiny, TransformUsageFlags.Dynamic)
                    });
            }
        }
    }
}