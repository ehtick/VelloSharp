using System;

namespace HarfBuzzSharp;

public delegate UnicodeCombiningClass CombiningClassDelegate(UnicodeFunctions functions, uint unicode);
public delegate UnicodeGeneralCategory GeneralCategoryDelegate(UnicodeFunctions functions, uint unicode);
public delegate uint MirroringDelegate(UnicodeFunctions functions, uint unicode);
public delegate Script ScriptDelegate(UnicodeFunctions functions, uint unicode);
public delegate bool ComposeDelegate(UnicodeFunctions functions, uint a, uint b, out uint ab);
public delegate bool DecomposeDelegate(UnicodeFunctions functions, uint ab, out uint a, out uint b);

public sealed class UnicodeFunctions : IDisposable
{
    private static readonly Lazy<UnicodeFunctions> LazyEmpty = new(() => new UnicodeFunctions(isImmutable: true));
    private bool _isImmutable;

    private CombiningClassDelegate? _combiningClass;
    private GeneralCategoryDelegate? _generalCategory;
    private MirroringDelegate? _mirroring;
    private ScriptDelegate? _script;
    private ComposeDelegate? _compose;
    private DecomposeDelegate? _decompose;

    private ReleaseDelegate? _combiningClassDestroy;
    private ReleaseDelegate? _generalCategoryDestroy;
    private ReleaseDelegate? _mirroringDestroy;
    private ReleaseDelegate? _scriptDestroy;
    private ReleaseDelegate? _composeDestroy;
    private ReleaseDelegate? _decomposeDestroy;

    public UnicodeFunctions()
    {
        _isImmutable = false;
    }

    private UnicodeFunctions(bool isImmutable)
    {
        _isImmutable = isImmutable;
    }

    public static UnicodeFunctions Empty => LazyEmpty.Value;

    public bool IsImmutable => _isImmutable;

    public CombiningClassDelegate? CombiningClass => _combiningClass;
    public GeneralCategoryDelegate? GeneralCategory => _generalCategory;
    public MirroringDelegate? Mirroring => _mirroring;
    public ScriptDelegate? Script => _script;
    public ComposeDelegate? Compose => _compose;
    public DecomposeDelegate? Decompose => _decompose;

    public void MakeImmutable()
    {
        _isImmutable = true;
    }

    public void SetCombiningClassDelegate(CombiningClassDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _combiningClass, ref _combiningClassDestroy, del, destroy);

    public void SetGeneralCategoryDelegate(GeneralCategoryDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _generalCategory, ref _generalCategoryDestroy, del, destroy);

    public void SetMirroringDelegate(MirroringDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _mirroring, ref _mirroringDestroy, del, destroy);

    public void SetScriptDelegate(ScriptDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _script, ref _scriptDestroy, del, destroy);

    public void SetComposeDelegate(ComposeDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _compose, ref _composeDestroy, del, destroy);

    public void SetDecomposeDelegate(DecomposeDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _decompose, ref _decomposeDestroy, del, destroy);

    public void Dispose()
    {
        DisposeDelegate(ref _combiningClassDestroy);
        DisposeDelegate(ref _generalCategoryDestroy);
        DisposeDelegate(ref _mirroringDestroy);
        DisposeDelegate(ref _scriptDestroy);
        DisposeDelegate(ref _composeDestroy);
        DisposeDelegate(ref _decomposeDestroy);

        _combiningClass = null;
        _generalCategory = null;
        _mirroring = null;
        _script = null;
        _compose = null;
        _decompose = null;
    }

    private void SetDelegate<TDelegate>(
        ref TDelegate? target,
        ref ReleaseDelegate? destroyField,
        TDelegate del,
        ReleaseDelegate? destroy)
        where TDelegate : class
    {
        if (del is null)
        {
            throw new ArgumentNullException(nameof(del));
        }

        if (_isImmutable)
        {
            throw new InvalidOperationException("UnicodeFunctions has been marked immutable.");
        }

        DisposeDelegate(ref destroyField);
        target = del;
        destroyField = destroy;
    }

    private static void DisposeDelegate(ref ReleaseDelegate? destroy)
    {
        try
        {
            destroy?.Invoke();
        }
        finally
        {
            destroy = null;
        }
    }
}

