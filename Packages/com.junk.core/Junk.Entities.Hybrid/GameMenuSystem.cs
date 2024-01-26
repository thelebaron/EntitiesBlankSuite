using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Junk.Entities.Hybrid
{
    
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct GameMenuSystem : ISystem
    {
        private float inputBlockTime;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnmanagedMouse>();
            state.RequireForUpdate<UnmanagedKeyboard>();
            //state.RequireForUpdate<Game>();

            if (!SystemAPI.HasSingleton<Game>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<GameSave>(entity);
                state.EntityManager.AddComponent<Game>(entity);
            }
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var keyboard = SystemAPI.GetSingleton<UnmanagedKeyboard>();
            foreach (var (game, menu, entity) in SystemAPI.Query<RefRW<Game>, RefRO<GameMenu>>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                game.ValueRW.InputLimiter -= SystemAPI.Time.DeltaTime;
                
                var escapeKeyIsPressed = keyboard.escapeKey.isPressed;
                
                if (escapeKeyIsPressed && game.ValueRW.InputLimiter <= 0)
                {
                    game.ValueRW.InputLimiter = 0.2f;
                    
                    
                    foreach (var menuRef in SystemAPI.Query<GameMenuRef>())
                    {
                        var enabledState = state.EntityManager.IsComponentEnabled<GameMenu>(entity);
                        var menuReference = state.EntityManager.GetComponentObject<GameMenuRef>(entity);

                        if (enabledState && !menu.ValueRO.PlayableSceneIsLoaded)
                        {
                            continue;
                        }
                        
                        state.EntityManager.SetComponentEnabled<GameMenu>(entity, !enabledState);
                    }

                    game.ValueRW.InputLimiter = 0.2f;
                }
            }
            
            foreach (var (game, menuRef, menu, entity) in SystemAPI.Query<RefRW<Game>, GameMenuRef, RefRO<GameMenu>>().WithEntityAccess().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                var menuIsEnabled = state.EntityManager.IsComponentEnabled<GameMenu>(entity);

                // Toggle menu
                menuRef.GameMenuBehaviour.Root.style.display = !menuIsEnabled ? DisplayStyle.None : DisplayStyle.Flex;
                game.ValueRW.MenuState                       = !menuIsEnabled ? Game.MenuGameplayState.Gameplay : Game.MenuGameplayState.GameMenu;

            }
        }
    }
}