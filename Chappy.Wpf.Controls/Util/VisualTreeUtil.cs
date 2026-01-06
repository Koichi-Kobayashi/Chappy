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
                if (d is T t)
                    return t;

                // LogicalTree（Run / TextElement 対策）
                if (d is FrameworkContentElement fce && fce.Parent is DependencyObject p1)
                {
                    d = p1;
                    continue;
                }

                var logical = LogicalTreeHelper.GetParent(d);
                if (logical != null)
                {
                    d = logical;
                    continue;
                }

                // VisualTree
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }
    }
}
