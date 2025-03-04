﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Blazorise.Demo.Pages.Tests;

public partial class TreeViewPage : ComponentBase
{
    private IList<NodeInfo> ExpandedNodes = new List<NodeInfo>();
    private NodeInfo selectedNode;

    public class NodeInfo
    {
        public string Text { get; set; }

        public IEnumerable<NodeInfo> Children { get; set; }
    }

    private IEnumerable<NodeInfo> Nodes = new[]
    {
        new NodeInfo { Text = "NodeInfo 1" },
        new NodeInfo
        {
            Text = "NodeInfo 2",
            Children = new []
            {
                new NodeInfo { Text = "NodeInfo 2.1" },
                new NodeInfo
                {
                    Text = "NodeInfo 2.2", Children = new []
                    {
                        new NodeInfo { Text = "NodeInfo 2.2.1" },
                        new NodeInfo { Text = "NodeInfo 2.2.2" },
                        new NodeInfo { Text = "NodeInfo 2.2.3" },
                        new NodeInfo { Text = "NodeInfo 2.2.4" }
                    }
                },
                new NodeInfo { Text = "NodeInfo 2.3" },
                new NodeInfo { Text = "NodeInfo 2.4" }
            }
        },
        new NodeInfo { Text = "NodeInfo 3" },
        new NodeInfo
        {
            Text = "NodeInfo 4",
            Children = new []
            {
                new NodeInfo { Text = "NodeInfo 4.1" },
                new NodeInfo
                {
                    Text = "NodeInfo 4.2", Children = new []
                    {
                        new NodeInfo { Text = "NodeInfo 4.2.1" },
                        new NodeInfo { Text = "NodeInfo 4.2.2" },
                        new NodeInfo { Text = "NodeInfo 4.2.3" },
                        new NodeInfo { Text = "NodeInfo 4.2.4" }
                    }
                },
                new NodeInfo { Text = "NodeInfo 4.3" },
                new NodeInfo { Text = "NodeInfo 4.4" }
            }
        },
        new NodeInfo { Text = "NodeInfo 5" },
        new NodeInfo { Text = "NodeInfo 6" }
    };
}