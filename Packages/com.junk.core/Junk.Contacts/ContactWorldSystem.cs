using System;
using Junk.Math;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Junk.Contacts
{
    public struct ContactWorldSingleton : IComponentData
    {
        public ContactWorld ContactWorld;

    }

    /// <summary>
    /// Contacts have several distinct sizes:
    /// Tiny - Bullet impacts, blood drips - 0.1m
    /// Small - Large caliber weapons - 0.25m
    /// Medium - Blood splatter - 0.5m
    /// Large - Craters - 1m
    /// </summary>
    public struct ContactWorld : IDisposable
    {
        [NoAlias] private  NativeArray<ContactData>                        m_Contacts;
        [NoAlias] private  NativeList<ContactData>                         m_PendingContacts;
        [NoAlias] internal NativeParallelHashMap<Entity, int>              EntityBodyIndexMap;
        public             NativeParallelMultiHashMap<int, ContactData>    TinyContactSpatialMap; // 0.1m
        [NoAlias] private  NativeParallelMultiHashMap<Entity, ContactData> m_ContactEntityIndexMap;
        private            PrefabEntityData                                m_PrefabEntityData;
        
        public  NativeArray<ContactData> Contacts        => m_Contacts;
        public  NativeList<ContactData>  PendingContacts => m_PendingContacts;
        public Entity BulletholeTinyPrefab => m_PrefabEntityData.BulletHoleTiny;
        
        public ContactWorld(int numStaticBodies, EntityManager entityManager)
        {
            m_Contacts = new NativeArray<ContactData>(numStaticBodies, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            
            //Broadphase = new Broadphase(numStaticBodies, numDynamicBodies);
            TinyContactSpatialMap = new NativeParallelMultiHashMap<int, ContactData>(m_Contacts.Length, Allocator.Persistent);
            EntityBodyIndexMap = new NativeParallelHashMap<Entity, int>(m_Contacts.Length, Allocator.Persistent);
            m_PendingContacts = new NativeList<ContactData>(Allocator.Persistent);
            m_ContactEntityIndexMap = new NativeParallelMultiHashMap<Entity, ContactData>(m_Contacts.Length, Allocator.Persistent);
            
            m_PrefabEntityData = default;
        }
        
        public void UpdatePrefabs(EntityManager entityManager)
        {
            // BovineLabs has a helper method but for now dont want to include another dependency
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<PrefabEntityData>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeSystems | EntityQueryOptions.Default)
                .Build(entityManager);
            query.CompleteDependency();
            if (query.CalculateEntityCount() < 1)
            {
                Debug.LogError("No PrefabEntityData found, please add a ContactsAuthoring to a subscene.");
                m_PrefabEntityData = default;
                return;
            }
            m_PrefabEntityData = query.GetSingleton<PrefabEntityData>();
        }
        
        /// <summary>
        /// Resize the internal arrays to the specified capacity.
        /// </summary>
        private void SetCapacity(int numBodies)
        {
            // Increase body storage if necessary
            if (m_Contacts.Length < numBodies)
            {
                m_Contacts.Dispose();
                m_Contacts = new NativeArray<ContactData>(numBodies, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                EntityBodyIndexMap.Capacity = m_Contacts.Length;
                TinyContactSpatialMap.Capacity = m_Contacts.Length;
                m_ContactEntityIndexMap.Capacity = m_Contacts.Length;
            }
        }
        
        public ContactData CreateContact(ContactType type, LocalToWorld localToWorld, RaycastHit hit = default, Entity parent = new Entity())
        {
            var contact = new ContactData
            {
                LocalToWorld = localToWorld,
                Parent       = parent,
                Type         = type,
                Hit          = hit
            };
            m_PendingContacts.Add(contact);
            return contact;
        }
        
        public void AddContact(Entity entity, ContactData contact)
        {
            m_ContactEntityIndexMap.Add(entity, contact);
        }
        
        public void Dispose()
        {
            m_Contacts.Dispose();
            EntityBodyIndexMap.Dispose();
            TinyContactSpatialMap.Dispose();
            m_PendingContacts.Dispose();
            m_ContactEntityIndexMap.Dispose();
        }
    }
    
    public struct ContactData : IComponentData
    {
        public LocalToWorld LocalToWorld;
        public ContactType  Type;
        public RaycastHit   Hit;
        public Entity       Parent;
    }
    
    public enum ContactType
    {
        DecalBulletTiny,
        DecalBulletSmall,
        DecalBloodSmall,
        DecalBloodMedium,
        DecalBloodLarge,
        DecalCrater
    }
    
    public partial struct ContactWorldSystem : ISystem, ISystemStartStop
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.AddComponentData(state.SystemHandle, new ContactWorldSingleton()
            {
                ContactWorld = new ContactWorld(100, state.EntityManager)
            });
            state.RequireForUpdate<PrefabEntityData>();
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            
            if(!SystemAPI.HasSingleton<PrefabEntityData>())
                Debug.Log("Missing singleton prefabentitydata.");
            var contactWorldSingleton = state.EntityManager.GetComponentData<ContactWorldSingleton>(state.SystemHandle);
            contactWorldSingleton.ContactWorld.UpdatePrefabs(state.EntityManager);
            state.EntityManager.SetComponentData(state.SystemHandle, contactWorldSingleton);
        }
        
        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
            
        }
        
        /// <summary>
        ///     Adds all npc entities to a HashMap(dictionary) with a positional location.
        ///     Should be fairly well optimized.
        /// </summary>
        [BurstCompile]
        private struct HashPositionJob : IJob
        {
            public EntityCommandBuffer CommandBuffer;
            public ContactWorld        ContactWorld;
            public Random              Random;
            public void Execute()
            {
                const float radiusTiny = 0.1f;
                //var incomingContacts =
                var contactsTiny = ContactWorld.TinyContactSpatialMap;
                for (var i = 0; i < ContactWorld.PendingContacts.Length; i++)
                {
                    var pendingContact = ContactWorld.PendingContacts[i];

                    switch (pendingContact.Type)
                    {                        
                        case ContactType.DecalBulletTiny:
                            
                            // Notes for future use - This(cellRadius) cannot be a different value per different faction,
                            // as a unique radius(the value used for hashing) filters out differently hashed entities, which for a general purpose targeting
                            // system is probably unwanted.
                            var hash = GetHashedPosition(pendingContact.LocalToWorld.Position, radiusTiny);

                            if (contactsTiny.ContainsKey(hash))
                            {
                                //Debug.Log(pendingContact.LocalToWorld.Position + " is already in the hashmap");
                                continue;
                            }
                            contactsTiny.Add(hash, pendingContact);
                            var contactEntity = CreateBulletholeDecal(hash, pendingContact); // note the entity is a deferred ecb entity and not valid unless remapped
                            break;
                        case ContactType.DecalBulletSmall:
                            break;
                        case ContactType.DecalBloodSmall:
                            break;
                        case ContactType.DecalBloodMedium:
                            break;
                        case ContactType.DecalBloodLarge:
                            break;
                        case ContactType.DecalCrater:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                ContactWorld.PendingContacts.Clear();
            }
            
            public Entity CreateBulletholeDecal(int positionHash, ContactData data)
            {
                // Get a random offset to combine with the normal to prevent z fighting
                var decal         = CommandBuffer.Instantiate(ContactWorld.BulletholeTinyPrefab);
                var randomOffset  = Random.NextFloat(0.002f, 0.01f);
                var randomScale   = Random.NextFloat(0.05f, 0.2f);
                var surfaceNormal = data.Hit.SurfaceNormal;
                
                if (surfaceNormal.Equals(maths.up) || surfaceNormal.Equals(-maths.up))
                {
                    var randomTilt = new float3(Random.NextFloat(-0.02f, 0.02f), 0, Random.NextFloat(-0.02f, 0.02f));
                    if (randomTilt.Equals(0f))
                        randomTilt = 0.03f;
                    
                    surfaceNormal += randomTilt;
                }

                var position = data.Hit.Position + surfaceNormal * randomOffset;
                var rot = math.mul(quaternion.LookRotationSafe(-surfaceNormal, maths.up), quaternion.Euler(0, 0, Random.NextFloat(0f, 360f)));

                var localTransform = LocalTransform.FromPositionRotationScale(position, rot, randomScale);
                // NOTE! normal must be negative(or depending on the facing dir of the quad being used)
                // otherwise the mesh shows as black even if both sides are rendered
                //EntityCommandBuffer.SetComponent(decal, new Rotation { Value = /*quaternion.LookRotationSafe(-surfaceNormal, maths.up)*/ });
                CommandBuffer.SetComponent(decal, localTransform);
                
                //EntityCommandBuffer.AddComponent(index, decal, new MaterialMainTexUv {Value = new float4(1, 1, 0, 0)});
                CommandBuffer.SetComponent(decal, new BulletDecalData {
                    Value        = 1337,//Count.Value, 
                    PositionHash = positionHash
                });
                
                //var count = Count.Value;// does this work?
                //count++;
                //Count.Value = count;
                
                return decal;
            }
        }

        /// <summary>
        ///     Iterate over hashmap and add potential targets that are within the same cell.
        ///     If the buffer has target/s in it, skip that entity. Otherwise attempt to add nearby entities.
        ///     Also dont add npcs that have the same team index todo: check for allied/hostile teams?
        /// </summary>
        [BurstCompile]
        private struct CheckHashedPositionsJob : IJobChunk
        {
            public            float                                        CellRadius;
            [ReadOnly] public EntityTypeHandle                             EntityTypeHandle;
            [ReadOnly] public ComponentLookup<LocalToWorld>                LocalToWorldData;
            [ReadOnly] public NativeParallelMultiHashMap<int, ContactData> HashMap;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in                 v128           chunkEnabledMask)
            {
                var entities       = chunk.GetNativeArray(EntityTypeHandle);
                for (var i = 0; i < chunk.Count; i++)
                {
                    var entity          = entities[i];

                    // Break out of target finding if theres already targets to sort
                    //if (targetEntities.Length > 0)
                        //continue;

                    // Notes for future use - if you get rid of this stored value, need to compute it at runtime and it must match the hash algorithm that was used to fill the hashmap
                    //var hash = personBehaviour.DebugSelfPositionHash;//(int) math.hash(new int3(math.floor(positon / personBehaviour.SightRadius)));
                    var hash         = GetHashedPosition(LocalToWorldData[entity].Position, CellRadius);
                    var containsHash = HashMap.ContainsKey(hash);

                    if (!containsHash)
                        continue;

                    var tryGetFirstValue = HashMap.TryGetFirstValue(hash, out var e, out var hashMapIterator);
                    if (tryGetFirstValue)
                    {
                        //if (e.Equals(entity))
                            //continue;
                        //if (e.Equals(Entity.Null))
                            //continue;
                        //Debug.Log("entity " + entity + " is trying to add " + e);
                        //targetEntities.Add(new TargetElement
                            //{ Value = new TargetInfo { Entity = e, Position = LocalToWorldData[e].Position } });
                    }

                    // Add more targets
                    for (var j = 0; j < 30; j++)
                        if (HashMap.TryGetNextValue(out e, ref hashMapIterator))
                        {
                            //if (e.Equals(entity))
                                //continue;
                            //if (e.Equals(Entity.Null))
                                //continue;
                            //Debug.Log("entity " + entity + " is trying to add " + e);
                            //targetEntities.Add(new TargetElement
                                //{ Value = new TargetInfo { Entity = e, Position = LocalToWorldData[e].Position } });
                        }
                        else
                        {
                            break;
                        }
                }
            }
        }
        
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new HashPositionJob
            {
                CommandBuffer = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
                ContactWorld = SystemAPI.GetSingletonRW<ContactWorldSingleton>().ValueRW.ContactWorld,
                Random = Random.CreateFromIndex(state.LastSystemVersion)
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            var contacts = state.EntityManager.GetComponentData<ContactWorldSingleton>(state.SystemHandle);
            contacts.ContactWorld.Dispose();
            state.EntityManager.RemoveComponent<ContactWorldSingleton>(state.SystemHandle);
        }
        
        private static int GetHashedPosition(float3 position, float cellRadius)
        {
            return (int)math.hash(new int3(math.floor(position / cellRadius)));
        }

    }
}