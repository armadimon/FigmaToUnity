using NUnit.Framework;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.FigmaApi;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaDataUtilsPageLookupTests
    {
        [Test]
        public void GetPageCanvasIdContainingNode_ReturnsCanvasId_ForDescendant()
        {
            var frame = new Node { id = "1:10", name = "Screen", type = NodeType.FRAME };
            var page = new Node
            {
                id = "0:1",
                name = "Page A",
                type = NodeType.CANVAS,
                children = new[] { frame }
            };
            var file = new FigmaFile
            {
                document = new Node
                {
                    id = "0:0",
                    name = "Document",
                    type = NodeType.DOCUMENT,
                    children = new[] { page }
                }
            };

            Assert.That(FigmaDataUtils.GetPageCanvasIdContainingNode(file, frame), Is.EqualTo("0:1"));
        }

        [Test]
        public void GetPageCanvasIdContainingNode_ReturnsSameId_WhenTargetIsPage()
        {
            var page = new Node { id = "0:2", name = "P", type = NodeType.CANVAS, children = null };
            var file = new FigmaFile
            {
                document = new Node
                {
                    id = "0:0",
                    name = "Document",
                    type = NodeType.DOCUMENT,
                    children = new[] { page }
                }
            };

            Assert.That(FigmaDataUtils.GetPageCanvasIdContainingNode(file, page), Is.EqualTo("0:2"));
        }

        [Test]
        public void GetScreenNodeIds_ExcludesNestedFramesThatAreNoLongerScreens()
        {
            var nestedFrame = new Node { id = "1:20", name = "Nested", type = NodeType.FRAME };
            var ownerFrame = new Node
            {
                id = "1:10",
                name = "Owner",
                type = NodeType.FRAME,
                children = new[] { nestedFrame }
            };
            var page = new Node
            {
                id = "0:1",
                name = "Page A",
                type = NodeType.CANVAS,
                children = new[] { ownerFrame }
            };
            var file = new FigmaFile
            {
                document = new Node
                {
                    id = "0:0",
                    name = "Document",
                    type = NodeType.DOCUMENT,
                    children = new[] { page }
                }
            };

            var screenIds = FigmaDataUtils.GetScreenNodeIds(file, new[] { page });

            Assert.That(screenIds.Contains("1:10"), Is.True);
            Assert.That(screenIds.Contains("1:20"), Is.False);
        }
    }
}
