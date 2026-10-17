using CombatSystem;
using NHance.Assets.Scripts;
using UnityEngine;

namespace NHanceCustomization
{
    /// <summary>
    /// Dresses a character with an <see cref="NHanceLookData"/>. Put it on the fighter (or a child) whose model
    /// has the N-Hance <c>NHAvatar</c>; the fighter calls it when it is initialized, before it places its weapons. A character with no
    /// fighter (an NPC that only lives an occupation, say) is dressed by the applier itself when the scene starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NHanceLookApplier : MonoBehaviour, IFighterAppearance
    {
        [SerializeField] private NHanceLookData _look;

        private NHAvatar _avatar;

        /// <summary>The look put on the character. Changing it does not dress the character; call <see cref="Apply"/>.</summary>
        public NHanceLookData Look
        {
            get => _look;
            set => _look = value;
        }

        // The avatar's own Start reads the Animator's controller without a null check and logs an error when there is none (a character
        // animated by the Combat System or the Occupation System has none). Dressing does not need Start, so the avatar is switched off here,
        // before any Start runs (only while playing, so no scene is changed).
        private void Awake()
        {
            if (FindAvatar() != null && _avatar.enabled)
            {
                _avatar.enabled = false;
            }
        }

        // A fighter dresses its character itself (ApplyAppearance); a character without one is dressed here.
        private void Start()
        {
            if (GetComponentInParent<Fighter>(true) == null && GetComponentInChildren<Fighter>(true) == null)
            {
                Apply();
            }
        }

        /// <summary>Called by the fighter when it is initialized.</summary>
        public void ApplyAppearance(Fighter fighter) => Apply();

        /// <summary>Dresses the character with <see cref="Look"/>. Does nothing when there is no look.</summary>
        public void Apply()
        {
            if (_look == null)
            {
                return;
            }

            if (FindAvatar() == null)
            {
                Debug.LogError($"[NHanceCustomization] '{name}' has no NHAvatar on it or under it, so '{_look.name}' cannot be applied.", this);
                return;
            }

            // Normally already off since Awake; this covers an applier that was added later.
            if (Application.isPlaying && _avatar.enabled)
            {
                _avatar.enabled = false;
            }

            NHanceLookRules.Apply(_avatar, _look);
#if HAS_GORE_LAB
            // A character that can be cut looks at what it wears now: the GoreLab cuts the worn pieces too.
            var gore = GetComponentInChildren<GoreLab.GoreCharacter>(true);
            if (gore == null)
            {
                gore = GetComponentInParent<GoreLab.GoreCharacter>(true);
            }

            if (gore != null)
            {
                gore.RefreshEquipment();
            }
#endif
        }

        private NHAvatar FindAvatar()
        {
            if (_avatar == null)
            {
                _avatar = GetComponentInChildren<NHAvatar>(true);
                if (_avatar == null)
                {
                    _avatar = GetComponentInParent<NHAvatar>(true);
                }
            }

            return _avatar;
        }
    }
}
