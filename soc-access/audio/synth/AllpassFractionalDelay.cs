// Ported from the Tanglebeep Tangledeep accessibility mod by Austin Hicks, used with the
// author's permission. Adapted to this repo's style and C# 7.3.
namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// First-order allpass fractional-delay filter. Realises a sub-sample delay of <c>d</c> samples
    /// (the part an integer sample shift cannot express) while leaving the magnitude response flat,
    /// so a tone passes through at full amplitude, just later.
    ///
    /// <para>Transfer function <c>H(z) = (a + z^-1) / (1 + a*z^-1)</c> with
    /// <c>a = (1 - d) / (1 + d)</c>; its phase delay at low frequency is <c>d</c> samples. The pole
    /// sits at <c>z = -a</c>, so accuracy and decay are best near <c>d = 1</c> and worst as
    /// <c>d</c> approaches 0 (pole approaches the unit circle, slow ringing). Callers keep <c>d</c>
    /// in <c>[0.5, 1.5)</c> so <c>|a| &lt;= 1/3</c> and the filter stays well behaved.</para>
    ///
    /// <para>One instance carries the state for one mono stream; feed samples in time order.</para>
    /// </summary>
    internal sealed class AllpassFractionalDelay
    {
        private readonly double _a;
        private double _previousInput;
        private double _previousOutput;

        /// <param name="fractionalDelay">Desired delay in samples; intended range [0.5, 1.5).</param>
        public AllpassFractionalDelay(double fractionalDelay)
        {
            _a = (1.0 - fractionalDelay) / (1.0 + fractionalDelay);
        }

        /// <summary>Push one input sample and get the delayed output: <c>y[n] = a*x[n] + x[n-1] - a*y[n-1]</c>.</summary>
        public float Process(float x)
        {
            double y = _a * x + _previousInput - _a * _previousOutput;
            _previousInput = x;
            _previousOutput = y;
            return (float)y;
        }
    }
}
