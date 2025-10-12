using System;

namespace HarfBuzzSharp;

public enum ClusterLevel
{
    MonotoneGraphemes = 0,
    MonotoneCharacters = 1,
    Characters = 2,
    Default = MonotoneGraphemes,
}

public enum ContentType
{
    Invalid = 0,
    Unicode = 1,
    Glyphs = 2,
}

[Flags]
public enum BufferDiffFlags
{
    Equal = 0,
    ContentTypeMismatch = 1 << 0,
    LengthMismatch = 1 << 1,
    NotdefPresent = 1 << 2,
    DottedCirclePresent = 1 << 3,
    CodepointMismatch = 1 << 4,
    ClusterMismatch = 1 << 5,
    GlyphFlagsMismatch = 1 << 6,
    PositionMismatch = 1 << 7,
}

[Flags]
public enum BufferFlags
{
    Default = 0,
    BeginningOfText = 1 << 0,
    EndOfText = 1 << 1,
    PreserveDefaultIgnorables = 1 << 2,
    RemoveDefaultIgnorables = 1 << 3,
    DoNotInsertDottedCircle = 1 << 4,
}

[Flags]
public enum SerializeFlag
{
    Default = 0,
    NoClusters = 1 << 0,
    NoPositions = 1 << 1,
    NoGlyphNames = 1 << 2,
    GlyphExtents = 1 << 3,
    GlyphFlags = 1 << 4,
    NoAdvances = 1 << 5,
}

public enum SerializeFormat
{
    Invalid = 0,
    Text = 1413830740,
    Json = 1246973774,
}

public enum Direction
{
    Invalid = 0,
    LeftToRight = 4,
    RightToLeft = 5,
    TopToBottom = 6,
    BottomToTop = 7,
}

[Flags]
public enum GlyphFlags
{
    UnsafeToBreak = 1 << 0,
    UnsafeToConcat = 1 << 1,
    SafeToInsertTatweel = 1 << 2,
    Defined = UnsafeToBreak | UnsafeToConcat | SafeToInsertTatweel,
}

public enum MemoryMode
{
    Duplicate = 0,
    ReadOnly = 1,
    Writeable = 2,
    ReadOnlyMayMakeWriteable = 3,
}

public enum OpenTypeMetricsTag
{
    HorizontalAscender = 1751216995,
    HorizontalDescender = 1751413603,
    HorizontalLineGap = 1751934832,
    HorizontalClippingAscent = 1751346273,
    HorizontalClippingDescent = 1751346276,
    VerticalAscender = 1986098019,
    VerticalDescender = 1986294627,
    VerticalLineGap = 1986815856,
    HorizontalCaretRise = 1751347827,
    HorizontalCaretRun = 1751347822,
    HorizontalCaretOffset = 1751347046,
    VerticalCaretRise = 1986228851,
    VerticalCaretRun = 1986228846,
    VerticalCaretOffset = 1986228070,
    XHeight = 2020108148,
    CapHeight = 1668311156,
    SubScriptEmXSize = 1935833203,
    SubScriptEmYSize = 1935833459,
    SubScriptEmXOffset = 1935833199,
    SubScriptEmYOffset = 1935833455,
    SuperScriptEmXSize = 1936750707,
    SuperScriptEmYSize = 1936750963,
    SuperScriptEmXOffset = 1936750703,
    SuperScriptEmYOffset = 1936750959,
    StrikeoutSize = 1937011315,
    StrikeoutOffset = 1937011311,
    UnderlineSize = 1970168947,
    UnderlineOffset = 1970168943,
}

[Flags]
public enum OpenTypeVarAxisFlags
{
    Hidden = 1,
}

public enum UnicodeCombiningClass
{
    NotReordered = 0,
    Overlay = 1,
    Nukta = 7,
    KanaVoicing = 8,
    Virama = 9,
    CCC10 = 10,
    CCC11 = 11,
    CCC12 = 12,
    CCC13 = 13,
    CCC14 = 14,
    CCC15 = 15,
    CCC16 = 16,
    CCC17 = 17,
    CCC18 = 18,
    CCC19 = 19,
    CCC20 = 20,
    CCC21 = 21,
    CCC22 = 22,
    CCC23 = 23,
    CCC24 = 24,
    CCC25 = 25,
    CCC26 = 26,
    CCC27 = 27,
    CCC28 = 28,
    CCC29 = 29,
    CCC30 = 30,
    CCC31 = 31,
    CCC32 = 32,
    CCC33 = 33,
    CCC34 = 34,
    CCC35 = 35,
    CCC36 = 36,
    CCC84 = 84,
    CCC91 = 91,
    CCC103 = 103,
    CCC107 = 107,
    CCC118 = 118,
    CCC122 = 122,
    CCC129 = 129,
    CCC130 = 130,
    CCC133 = 132,
    AttachedBelowLeft = 200,
    AttachedBelow = 202,
    AttachedAbove = 214,
    AttachedAboveRight = 216,
    BelowLeft = 218,
    Below = 220,
    BelowRight = 222,
    Left = 224,
    Right = 226,
    AboveLeft = 228,
    Above = 230,
    AboveRight = 232,
    DoubleBelow = 233,
    DoubleAbove = 234,
    IotaSubscript = 240,
    Invalid = 255,
}

public enum UnicodeGeneralCategory
{
    Control = 0,
    Format = 1,
    Unassigned = 2,
    PrivateUse = 3,
    Surrogate = 4,
    LowercaseLetter = 5,
    ModifierLetter = 6,
    OtherLetter = 7,
    TitlecaseLetter = 8,
    UppercaseLetter = 9,
    SpacingMark = 10,
    EnclosingMark = 11,
    NonSpacingMark = 12,
    DecimalNumber = 13,
    LetterNumber = 14,
    OtherNumber = 15,
    ConnectPunctuation = 16,
    DashPunctuation = 17,
    ClosePunctuation = 18,
    FinalPunctuation = 19,
    InitialPunctuation = 20,
    OtherPunctuation = 21,
    OpenPunctuation = 22,
    CurrencySymbol = 23,
    ModifierSymbol = 24,
    MathSymbol = 25,
    OtherSymbol = 26,
    LineSeparator = 27,
    ParagraphSeparator = 28,
    SpaceSeparator = 29,
}
