using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ControlCatalog.ViewModels
{
    public class CursorPageViewModel
    {
        public CursorPageViewModel()
        {
            StandardCursors = Enum.GetValues(typeof(StandardCursorType))
                .Cast<StandardCursorType>()
                .Select(x => new StandardCursorModel(x))
                .ToList();

            Cursor? cursor = null;
            try
            {
                using var stream = AssetLoader.Open(new Uri("avares://ControlCatalog/Assets/avalonia-32.png"));
                var bitmap = new Bitmap(stream);
                cursor = new Cursor(bitmap, new PixelPoint(16, 16));
            }
            catch (NotSupportedException)
            {
                cursor = Cursor.Default;
            }

            CustomCursor = cursor ?? Cursor.Default;
        }

        public IEnumerable<StandardCursorModel> StandardCursors { get; }
        
        public Cursor CustomCursor { get; }
    }
    
    public class StandardCursorModel
    {
        public StandardCursorModel(StandardCursorType type)
        {
            Type = type;
            Cursor = new Cursor(type);
        }

        public StandardCursorType Type { get; }
            
        public Cursor Cursor { get; }
    }
}
