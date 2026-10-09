using CombatSystem;
using NHance.Assets.Scripts;
using UnityEngine;

namespace NHanceLook
{
    /// <summary>
    /// Dresses a character with an <see cref="NHanceLookData"/>. Put it on the fighter (or a child) whose model
    /// has the N-Hance <c>NHAvatar</c>; the fighter calls it when it is initialized, before it places its weapons.
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

        /// <summary>Called by the fighter when it is initialized.</summary>
        public void ApplyAppearance(Fighter fighter) => Apply();

        /// <summary>Dresses the character with <see cref="Look"/>. Does nothing when there is no look.</summary>
        public void Apply()
        {
            if (_look == null)
            {
                return;
            }

            if (_avatar == null)
            {
                _avatar = GetComponentInChildren<NHAvatar>(true);
                if (_avatar == null)
                {
                    _avatar = GetComponentInParent<NHAvatar>(true);
                }
            }

            if (_avatar == null)
            {
                Debug.LogError($"[NHanceLook] '{name}' has no NHAvatar on it or under it, so '{_look.name}' cannot be applied.", this);
                return;
            }

            // The avatar's own Start only prepares N-Hance's animation browser and reads the Animator's controller without a
            // null check: a character animated by the Combat System has none and it would log an error. Dressing does not need
            // Start, so the avatar stays switched off (done before its first Start; only while playing, so no scene is changed).
            if (Application.isPlaying && _avatar.enabled)
            {
                _avatar.enabled = false;
            }

            NHanceLookRules.Apply(_avatar, _look);
        }
    }
}
