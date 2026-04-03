using System.Collections.Generic;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface ICommandRecognizer
{
    void UpdateTemplates(IReadOnlyDictionary<VocalAction, ActionTemplate> templates);
    void Reset();
    DirectionalRecognitionResult Recognize(DirectionalRecognitionInput input);
}
