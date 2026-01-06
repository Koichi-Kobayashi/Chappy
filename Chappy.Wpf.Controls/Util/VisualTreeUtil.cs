using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.Util
{
    public static class VisualTreeUtil
    {
        /// <summary>
        /// 指定された DependencyObject から親方向にたどって、
        /// 最初に見つかった指定型の要素を返す
        /// （Visual / Logical / FrameworkContentElement 対応）
        /// </summary>
        public static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;

                // FrameworkContentElement（Run/TextElement等）
                if (d is System.Windows.FrameworkContentElement fce && fce.Parent is DependencyObject pFce)
                {
                    d = pFce;
                    continue;
                }

                // Visual を優先
                var visualParent = VisualTreeHelper.GetParent(d);
                if (visualParent != null)
                {
                    d = visualParent;
                    continue;
                }

                // Visual が取れない時だけ Logical
                d = LogicalTreeHelper.GetParent(d);
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
