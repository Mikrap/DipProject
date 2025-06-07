using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace DiplomV3.Pages
{
    public class DataTemplateFactory
    {
        private readonly Func<FrameworkElement> _factory;

        public DataTemplateFactory(Func<FrameworkElement> factory)
        {
            _factory = factory;
        }

        public DataTemplate CreateTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(ContentPresenter));
            factory.SetValue(ContentPresenter.ContentProperty, _factory());
            template.VisualTree = factory;
            return template;
        }

        public static implicit operator DataTemplate(DataTemplateFactory factory)
        {
            return factory.CreateTemplate();
        }
    }
}
