using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface ICommandRecognizer
{
    Task<RecognitionResult?> RecognizeAsync(float[] features, CancellationToken cancellationToken);
}
