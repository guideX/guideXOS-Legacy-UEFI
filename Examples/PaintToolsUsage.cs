// Paint Tool Usage Example
// This example demonstrates how to use the enhanced Paint application

using guideXOS.DefaultApps;
using guideXOS.GUI;

namespace PaintExamples {
    /// <summary>
    /// Example: Launching the Paint application
    /// </summary>
    public class PaintExample {
        public static void LaunchPaint() {
            // Create a new Paint window at position (100, 100)
            Paint paintWindow = new Paint(100, 100);
            
            // The window will automatically:
            // - Start with Brush tool selected
            // - Default brush size of 5px
            // - White (0xFFF0F0F0) as initial color
            // - Dark gray (0xFF222222) as background
            
            // Add to window manager
            WindowManager.Add(paintWindow);
        }
    }
    
    /// <summary>
    /// Example: Understanding tool behavior
    /// </summary>
    public class ToolBehaviorExample {
        public static void DemonstratePencil() {
            // PENCIL TOOL
            // - Draws single pixels
            // - No interpolation
            // - Best for: Pixel art, precise details
            // - Size setting: Ignored (always 1px)
        }
        
        public static void DemonstrateBrush() {
            // BRUSH TOOL
            // - Circular brush shape
            // - Smooth interpolation between points
            // - Best for: General drawing, painting
            // - Size setting: 1, 3, 5, 10, or 20 pixels
            // - Default tool on startup
        }
        
        public static void DemonstrateEraser() {
            // ERASER TOOL
            // - Replaces pixels with background color
            // - Uses same interpolation as brush
            // - Best for: Correcting mistakes
            // - Size setting: 1, 3, 5, 10, or 20 pixels
        }
        
        public static void DemonstrateLine() {
            // LINE TOOL
            // Usage:
            // 1. Click to set start point
            // 2. Drag mouse to end position
            // 3. Release to draw line
            // 
            // Features:
            // - Straight lines only
            // - Thickness controlled by brush size
            // - Anti-aliased for smooth appearance
            // - Best for: Diagrams, geometric art
        }
        
        public static void DemonstrateRectangle() {
            // RECTANGLE TOOL
            // Usage:
            // 1. Click to set one corner
            // 2. Drag to opposite corner
            // 3. Release to draw rectangle
            //
            // Features:
            // - Hollow rectangle (outline only)
            // - Auto-normalizes if dragged in reverse
            // - Thickness controlled by brush size
            // - Best for: Boxes, frames, layouts
        }
        
        public static void DemonstrateCircle() {
            // CIRCLE TOOL
            // Usage:
            // 1. Click to set center point
            // 2. Drag outward to set radius
            // 3. Release to draw circle
            //
            // Features:
            // - Hollow circle (outline only)
            // - Radius based on distance from center
            // - Thickness controlled by brush size
            // - Uses midpoint circle algorithm
            // - Best for: Round shapes, diagrams
        }
        
        public static void DemonstrateFillBucket() {
            // FILL BUCKET TOOL
            // Usage:
            // 1. Select fill color from palette
            // 2. Click on region to fill
            //
            // Features:
            // - Fills connected pixels of same color
            // - 4-way connectivity (up/down/left/right)
            // - Safety limit: 50,000 pixels max
            // - Best for: Coloring large areas
            //
            // Note: If region is too large (>50k pixels),
            //       fill will stop at limit
        }
    }
    
    /// <summary>
    /// Example: Typical workflow
    /// </summary>
    public class WorkflowExample {
        public static void CreateSimpleDrawing() {
            /*
             * WORKFLOW: Creating a simple house drawing
             * 
             * 1. Select Rectangle tool
             * 2. Choose Brown color
             * 3. Draw house body (large rectangle)
             * 
             * 4. Select Line tool
             * 5. Choose Brown color
             * 6. Draw roof (two diagonal lines forming triangle)
             * 
             * 7. Select Rectangle tool
             * 8. Choose Blue color
             * 9. Draw door (small rectangle)
             * 
             * 10. Select Circle tool
             * 11. Choose Yellow color
             * 12. Draw sun in corner
             * 
             * 13. Select Fill Bucket
             * 14. Choose Light Blue
             * 15. Fill sky area
             * 
             * 16. Select Fill Bucket
             * 17. Choose Green
             * 18. Fill grass area
             * 
             * 19. Select Brush tool (size 3px)
             * 20. Choose Green
             * 21. Add grass details
             * 
             * 22. Done!
             */
        }
        
        public static void CreatePixelArt() {
            /*
             * WORKFLOW: Creating 16x16 pixel art sprite
             * 
             * 1. Select Pencil tool (1px precision)
             * 2. Choose Black color
             * 3. Draw outline of sprite
             * 
             * 4. Select Fill Bucket
             * 5. Choose Base color
             * 6. Fill main body
             * 
             * 7. Select Pencil tool
             * 8. Choose Highlight color
             * 9. Add highlights pixel by pixel
             * 
             * 10. Select Pencil tool
             * 11. Choose Shadow color
             * 12. Add shadows pixel by pixel
             * 
             * 13. Done!
             */
        }
    }
    
    /// <summary>
    /// Example: Keyboard shortcuts (future enhancement)
    /// </summary>
    public class KeyboardShortcuts {
        /*
         * PROPOSED SHORTCUTS (not yet implemented):
         * 
         * Tools:
         * P - Pencil
         * B - Brush
         * E - Eraser
         * L - Line
         * R - Rectangle
         * C - Circle
         * F - Fill Bucket
         * 
         * Brush Sizes:
         * 1 - 1px
         * 2 - 3px
         * 3 - 5px
         * 4 - 10px
         * 5 - 20px
         * 
         * Actions:
         * Ctrl+Z - Undo (future)
         * Ctrl+Y - Redo (future)
         * Ctrl+S - Save (future)
         * Ctrl+O - Open (future)
         * Ctrl+N - New (Clear)
         * 
         * View:
         * + - Zoom In (future)
         * - - Zoom Out (future)
         * 0 - Zoom 100% (future)
         * G - Toggle Grid (future)
         */
    }
    
    /// <summary>
    /// Example: Tips and tricks
    /// </summary>
    public class TipsAndTricks {
        /*
         * TIP 1: Smooth Lines
         * - Use Brush tool with size 1-3 for smooth freehand lines
         * - Use Line tool for perfectly straight lines
         * 
         * TIP 2: Perfect Shapes
         * - Rectangle and Circle tools auto-normalize dimensions
         * - You can drag in any direction
         * 
         * TIP 3: Large Areas
         * - Use Fill Bucket for large solid areas
         * - Faster than painting with brush
         * 
         * TIP 4: Detail Work
         * - Switch to Pencil for pixel-perfect control
         * - Zoom in if needed (future feature)
         * 
         * TIP 5: Color Selection
         * - Current color shown with yellow border
         * - White (0xFFECF0F1) good for highlights
         * - Black (0xFF000000) good for outlines
         * 
         * TIP 6: Brush Sizes
         * - Size 1: Detail work, pixel art
         * - Size 3-5: General drawing
         * - Size 10-20: Bold strokes, filling
         * 
         * TIP 7: Eraser Usage
         * - Eraser uses same sizes as brush
         * - Erases to background color (dark gray)
         * - Use large eraser to clear sections quickly
         * 
         * TIP 8: Tool Workflow
         * - Sketch with Pencil or small Brush
         * - Outline with Line/Rectangle/Circle tools
         * - Fill areas with Fill Bucket
         * - Add details with Pencil
         * 
         * TIP 9: Avoiding Mistakes
         * - Plan your drawing before starting
         * - Use Light colors first, darker for outlines
         * - Remember: No undo (yet!), so work carefully
         * 
         * TIP 10: Performance
         * - Fill Bucket may pause on large regions
         * - This is normal (50k pixel limit)
         * - Use smaller fills or multiple clicks
         */
    }
    
    /// <summary>
    /// Example: Common issues and solutions
    /// </summary>
    public class Troubleshooting {
        /*
         * ISSUE: Flood fill doesn't fill entire area
         * SOLUTION: Region may exceed 50,000 pixel limit.
         *           Use multiple fills in different sections.
         * 
         * ISSUE: Brush drawing has gaps
         * SOLUTION: Drawing too fast. The brush interpolates
         *           between positions, but extremely fast
         *           movement may skip pixels.
         * 
         * ISSUE: Can't select certain colors
         * SOLUTION: Color must be clicked precisely within
         *           the color button bounds.
         * 
         * ISSUE: Tools not drawing
         * SOLUTION: Ensure mouse is in canvas area (below
         *           toolbar). Toolbar area reserved for UI.
         * 
         * ISSUE: Shapes not appearing
         * SOLUTION: Line/Rectangle/Circle require click-drag-
         *           release. Hold mouse down while dragging.
         * 
         * ISSUE: Eraser not working
         * SOLUTION: Eraser erases to background color (dark
         *           gray). If drawing on dark gray, result
         *           appears invisible.
         * 
         * ISSUE: Clear button not clearing
         * SOLUTION: Clear button is in top toolbar, right side.
         *           Clears entire canvas to background color.
         */
    }
}
