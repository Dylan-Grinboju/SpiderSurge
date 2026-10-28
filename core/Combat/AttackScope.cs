using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // One part's entries on the attack stack: only the methods it registered, innermost at Top
    public class AttackView
    {
        private static int _views;

        internal readonly uint Bit;

        public AttackView()
        {
            if (_views == 32)
            {
                throw new InvalidOperationException("AttackScope supports at most 32 views");
            }
            Bit = 1u << _views++;
        }

        public int Top { get; internal set; } = -1;

        public bool Active => Top >= 0;

        public Component Source => Top >= 0 ? AttackScope.SourceAt(Top) : null;

        public Component SourceAt(int index) => AttackScope.SourceAt(index);

        public int Below(int index) => AttackScope.Below(Bit, index);

        // The innermost of this view's entries whose source is a T, or -1
        public int Innermost<T>() where T : Component
        {
            for (int index = Top; index >= 0; index = Below(index))
            {
                if (AttackScope.SourceAt(index) is T)
                {
                    return index;
                }
            }
            return -1;
        }

        public void Register(MethodBase method, object info = null) => AttackScope.Register(this, method, info);

        public void Unregister() => AttackScope.Unregister(this);

        internal virtual bool Open(int index, Component source, object info) => true;
    }

    // A view that keeps a value per entry, filled when the entry is pushed
    public sealed class AttackView<T> : AttackView where T : struct
    {
        public delegate bool Opener(ref T slot, Component source, object info);

        private readonly Opener _open;
        private T[] _slots = new T[8];

        public AttackView(Opener open)
        {
            _open = open;
        }

        public ref T Slot(int index) => ref _slots[index];

        internal override bool Open(int index, Component source, object info)
        {
            if (index >= _slots.Length)
            {
                Array.Resize(ref _slots, Math.Max(index + 1, _slots.Length * 2));
            }
            _slots[index] = default;
            return _open(ref _slots[index], source, info);
        }
    }

    // Damage and Impact have no attacker argument, so one prefix and finalizer push every registered attacker method here
    public static class AttackScope
    {
        private struct Frame
        {
            public Component Source;
            public uint Views;
        }

        private sealed class Target
        {
            public readonly List<AttackView> Views = new List<AttackView>(4);
            public readonly List<object> Infos = new List<object>(4);
        }

        private static readonly Dictionary<MethodBase, Target> Targets = new Dictionary<MethodBase, Target>();
        private static readonly List<AttackView> Views = new List<AttackView>();
        private static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>();
        private static Frame[] _frames = new Frame[8];
        private static int _count;

        internal static Component SourceAt(int index) => _frames[index].Source;

        internal static int Below(uint bit, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if ((_frames[i].Views & bit) != 0)
                {
                    return i;
                }
            }
            return -1;
        }

        internal static void Register(AttackView view, MethodBase method, object info)
        {
            if (method == null)
            {
                return;
            }

            if (!Targets.TryGetValue(method, out Target target))
            {
                Targets[method] = target = new Target();
            }
            int existing = target.Views.IndexOf(view);
            if (existing >= 0)
            {
                target.Infos[existing] = info;
            }
            else
            {
                target.Views.Add(view);
                target.Infos.Add(info);
            }
            if (!Views.Contains(view))
            {
                Views.Add(view);
            }
        }

        internal static void Unregister(AttackView view)
        {
            foreach (Target target in Targets.Values)
            {
                int index = target.Views.IndexOf(view);
                if (index >= 0)
                {
                    target.Views.RemoveAt(index);
                    target.Infos.RemoveAt(index);
                }
            }
            Views.Remove(view);
            view.Top = -1;
        }

        // Runs once every part has registered its methods
        internal static void Patch(Harmony harmony)
        {
            var prefix = new HarmonyMethod(AccessTools.Method(typeof(AttackScope), nameof(Prefix))) { priority = Priority.First };
            var finalizer = new HarmonyMethod(AccessTools.Method(typeof(AttackScope), nameof(Finalizer))) { priority = Priority.First };
            foreach (MethodBase method in Targets.Keys)
            {
                if (!Patched.Add(method))
                {
                    continue;
                }
                try
                {
                    harmony.Patch(method, prefix: prefix, finalizer: finalizer);
                }
                catch (Exception ex)
                {
                    Patched.Remove(method);
                    Logger.LogError($"core could not scope {method.DeclaringType?.Name}.{method.Name}: {ex.GetBaseException().Message}");
                }
            }
            Logger.LogInfo($"core scoped {Patched.Count} attacker methods");
        }

        // __state is the depth after the push, so 0 means nothing was pushed, as when an earlier prefix threw
        private static void Prefix(Component __instance, MethodBase __originalMethod, out int __state)
        {
            __state = 0;
            if (!Targets.TryGetValue(__originalMethod, out Target target) || target.Views.Count == 0)
            {
                return;
            }

            int index = _count;
            if (index == _frames.Length)
            {
                Array.Resize(ref _frames, index * 2);
            }

            uint views = 0;
            for (int i = 0; i < target.Views.Count; i++)
            {
                AttackView view = target.Views[i];
                try
                {
                    if (view.Open(index, __instance, target.Infos[i]))
                    {
                        views |= view.Bit;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Attack scope for {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name} failed: {ex.Message}");
                }
            }

            _frames[index] = new Frame { Source = __instance, Views = views };
            _count = index + 1;
            for (int i = 0; i < target.Views.Count; i++)
            {
                AttackView view = target.Views[i];
                if ((views & view.Bit) != 0)
                {
                    view.Top = index;
                }
            }
            __state = _count;
        }

        private static void Finalizer(int __state)
        {
            if (__state == 0 || __state > _count)
            {
                return;
            }

            int index = __state - 1;
            uint popped = 0;
            for (int i = index; i < _count; i++)
            {
                popped |= _frames[i].Views;
                _frames[i].Source = null;
            }
            _count = index;
            if (popped == 0)
            {
                return;
            }
            foreach (AttackView view in Views)
            {
                if ((popped & view.Bit) != 0 && view.Top >= index)
                {
                    view.Top = Below(view.Bit, index);
                }
            }
        }
    }
}
