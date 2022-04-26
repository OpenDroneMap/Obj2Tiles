namespace Obj2Tiles.Library.Materials;

public enum IlluminationModel
{
    ColorOnAmbientOff = 0,
    ColorOnAmbientOn = 1,
    HighlightOn = 2,
    ReflectionOnRayTraceOn = 3,
    TransparencyGlassOnReflectionRayTraceOn = 4,
    ReflectionFresnelOnRayTraceOn = 5,
    TransparencyRefractionOnReflectionFresnelOffRayTraceOn = 6,
    TransparencyRefractionOnReflectionFresnelOnRayTraceOn = 7,
    ReflectionOn = 8,
    TransparencyGlassOnReflectionRayTraceOff = 9,
    CastsShadows = 10
}