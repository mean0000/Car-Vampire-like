// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.
/// <summary>
/// A ScriptableObject that defines a complete, multi-track animation and its associated data, 
/// including playback settings, preview models, and custom action blocks.
/// </summary>
/// <remarks>
/// This asset is used by the <see cref="AnimCoordinatorComponent"/> to play animations 
/// with custom logic and behavior beyond a standard Unity AnimationClip.
/// </remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Jorjouto.AnimComposerSystem
{
    public class StringListSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private string[] listItems;
        private Action<string> onSetIndexCallback;

        public void Init(string[] items, Action<string> callback)
        {
            listItems = items;
            onSetIndexCallback = callback;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> searchList = new()
            {
                new SearchTreeGroupEntry(new GUIContent("Bones List"), 0)
            };

            List<string> groups = new();

            foreach(string item in listItems)
            {
                string[] entryTitle = item.Split('/');
                string groupName = "";

                for(int i = 0; i < entryTitle.Length - 1; i++)
                {
                    groupName += entryTitle[i];
                    if(!groups.Contains(groupName))
                    {
                        searchList.Add(new SearchTreeGroupEntry(new GUIContent(entryTitle[i]), i + 1));
                        groups.Add(groupName);
                    }   
                    groupName += "/";
                }

                SearchTreeEntry entry = new(new GUIContent(entryTitle.Last()))
                {
                    level = entryTitle.Length,
                    userData = entryTitle.Last()
                };

                searchList.Add(entry);
            }

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            onSetIndexCallback?.Invoke((string)SearchTreeEntry.userData);
            return true;
        }
    }
}