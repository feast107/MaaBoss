namespace MaaBoss.Core.Models;

public enum FlowRecognitionType
{
    DirectHit,
    TemplateMatch,
    FeatureMatch,
    ColorMatch,
    OCR,
    NeuralNetworkClassify,
    NeuralNetworkDetect,
    And,
    Or,
    Custom,
}
