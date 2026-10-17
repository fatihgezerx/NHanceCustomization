#if HAS_VENEER
using NHance.Assets.Scripts.Enums;

namespace NHanceCustomization
{
    /// <summary>
    /// The computed state of one <see cref="NHanceLookData"/> for its Inspector: the header pill (how many slots hold an item, or a
    /// base mismatch) and the warning about a Preview Base made for another gender. Strings are cached, so the 200 ms providers allocate
    /// nothing while nothing changes.
    /// </summary>
    internal sealed class NHanceLookDataStatus
    {
        private readonly NHanceLookData look;

        private int cachedFilled = -1;
        private string cachedFilledText = string.Empty;
        private string cachedMismatch;
        private Gender cachedMismatchBase;
        private Gender cachedMismatchLook;

        /// <summary>Makes the status of <paramref name="look"/>.</summary>
        public NHanceLookDataStatus(NHanceLookData look)
        {
            this.look = look;
        }

        /// <summary>The header pill: the filled item count (<c>idle</c> when none), or "Base mismatch" (<c>warn</c>).</summary>
        public (string text, string kind) PillState()
        {
            if (look == null)
            {
                return (string.Empty, "idle");
            }

            if (look.PreviewBase != null && look.PreviewBase.Gender != look.Gender)
            {
                return ("Base mismatch", "warn");
            }

            int filled = 0;
            for (int i = 0; i < look.Slots.Count; i++)
            {
                if (look.Slots[i].Item != null)
                {
                    filled++;
                }
            }

            if (filled != cachedFilled)
            {
                cachedFilled = filled;
                cachedFilledText = filled == 1 ? "1 item" : filled + " items";
            }

            return (cachedFilledText, filled == 0 ? "idle" : "ok");
        }

        /// <summary>The warning text while the Preview Base is set up for another gender than the look; null otherwise.</summary>
        public string MismatchText()
        {
            if (look == null || look.PreviewBase == null || look.PreviewBase.Gender == look.Gender)
            {
                return null;
            }

            Gender previewGender = look.PreviewBase.Gender;
            if (cachedMismatch == null || cachedMismatchBase != previewGender || cachedMismatchLook != look.Gender)
            {
                cachedMismatchBase = previewGender;
                cachedMismatchLook = look.Gender;
                cachedMismatch = $"The Preview Base is set up as {previewGender} but this look is {look.Gender}: its items will not fit the body. " +
                                 "Change Race & Gender, or use another Preview Base.";
            }

            return cachedMismatch;
        }
    }
}
#endif
