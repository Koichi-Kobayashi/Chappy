using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.Util
{
    public static class VirtualTreeUtil
    {
        /// <summary>
        /// 指定された DependencyObject から親方向にたどって、
        /// 最初に見つかった指定型の要素を返す
        /// （Visual / Logical / FrameworkContentElement 対応）
        /// </summary>
        public static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
        {
            DependencyObject? current = d;

            while (current != null)
            {
                if (current is T target)
                    return target;

                // Visual tree (Visual / Visual3D)
                if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                {
                    current = VisualTreeHelper.GetParent(current);
                    continue;
                }

                // FrameworkContentElement (Run など) は VisualTreeHelper.GetParent が取れないことがある
                if (current is FrameworkContentElement fce && fce.Parent is DependencyObject fceParent)
                {
                    current = fceParent;
                    continue;
                }

                // Fallback to logical tree
                current = LogicalTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 指定されてた DependencyObject から子方向にたどって、
        /// 最初に見つかった指定型の要素を返す
        /// </summary>
        public static T? FindDescendant<T>(DependencyObject d) where T : DependencyObject
        {
            if (d == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            {
                var child = VisualTreeHelper.GetChild(d, i);
                if (child is T t) return t;

                var found = FindDescendant<T>(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
