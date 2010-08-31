﻿using System.Collections.Generic;
using System.Linq;
using Autofac;
using Moq;
using NUnit.Framework;
using Orchard.DisplayManagement;
using Orchard.DisplayManagement.Descriptors;
using Orchard.DisplayManagement.Descriptors.ShapeAttributeStrategy;
using Orchard.DisplayManagement.Implementation;
using Orchard.Environment;
using Orchard.Environment.Extensions.Models;
using Orchard.Tests.Utility;

namespace Orchard.Tests.DisplayManagement.Descriptors {
    [TestFixture]
    public class ShapeAttributeBindingStrategyTests : ContainerTestBase {
        private FeatureDescriptor _testFeature;

        protected override void Register(Autofac.ContainerBuilder builder) {
            builder.RegisterAutoMocking();
            _testFeature = new FeatureDescriptor { Name = "Testing", Extension = new ExtensionDescriptor { Name = "Testing" } };
            builder.RegisterType<ShapeAttributeBindingStrategy>().As<IShapeDescriptorBindingStrategy>();
            builder.RegisterInstance(new TestProvider()).WithMetadata("Feature", _testFeature);
            builder.RegisterModule(new ShapeAttributeBindingModule());
        }

        protected override void Resolve(IContainer container) {
            // implementation resorts to orchard host to resolve "current scope" services
            container.Resolve<Mock<IOrchardHostContainer>>()
                .Setup(x => x.Resolve<IComponentContext>())
                .Returns(container);
        }

        class TestProvider {
            [Shape]
            public string Simple() {
                return "Simple";
            }

            [Shape("Renamed")]
            public string RenamedMethod() {
                return "Renamed";
            }
        }

        private IEnumerable<ShapeDescriptorAlteration> GetInitializers() {
            var strategy = _container.Resolve<IShapeDescriptorBindingStrategy>();
            var builder = new ShapeTableBuilder();
            strategy.Discover(builder);
            return builder.Build();
        }

        [Test]
        public void ShapeAttributeOccurrencesAreDetected() {
            var occurrences = _container.Resolve<IEnumerable<ShapeAttributeOccurrence>>();
            Assert.That(occurrences.Any(o => o.MethodInfo == typeof(TestProvider).GetMethod("Simple")));
        }

        [Test]
        public void InitializersHaveExpectedShapeTypeNames() {
            var strategy = _container.Resolve<IShapeDescriptorBindingStrategy>();
            var builder = new ShapeTableBuilder();
            strategy.Discover(builder);
            var initializers = builder.Build();
            Assert.That(initializers.Any(i => i.ShapeType == "Simple"));
            Assert.That(initializers.Any(i => i.ShapeType == "Renamed"));
            Assert.That(initializers.Any(i => i.ShapeType == "RenamedMethod"), Is.False);
        }

        [Test]
        public void FeatureMetadataIsDetected() {
            var strategy = _container.Resolve<IShapeDescriptorBindingStrategy>();
            var builder = new ShapeTableBuilder();
            strategy.Discover(builder);
            var initializers = builder.Build();
            Assert.That(initializers.All(i => i.Feature == _testFeature));
        }

        [Test]
        public void LifetimeScopeContainersHaveMetadata() {
            var strategy = _container.Resolve<IShapeDescriptorBindingStrategy>();
            var builder = new ShapeTableBuilder();
            strategy.Discover(builder);
            var initializers = builder.Build();
            Assert.That(initializers.Any(i => i.ShapeType == "Simple"));

            var childContainer = _container.BeginLifetimeScope();

            var strategy2 = childContainer.Resolve<IShapeDescriptorBindingStrategy>();
            var builder2 = new ShapeTableBuilder();
            strategy2.Discover(builder2);
            var initializers2 = builder2.Build();
            Assert.That(initializers2.Any(i => i.ShapeType == "Simple"));

            Assert.That(strategy, Is.Not.SameAs(strategy2));
        }

        [Test]
        public void BindingProvidedByStrategyInvokesMethod() {
            var initializers = GetInitializers();

            var shapeDescriptor = initializers.Where(i => i.ShapeType == "Simple")
                .Aggregate(new ShapeDescriptor { ShapeType = "Simple" }, (d, i) => { i.Alter(d); return d; });

            var displayContext = new DisplayContext();
            var result = shapeDescriptor.Binding(displayContext);
            var result2 = shapeDescriptor.Binding.Invoke(displayContext);
            Assert.That(result.ToString(), Is.StringContaining("Simple"));
            Assert.That(result2.ToString(), Is.StringContaining("Simple"));
        }

    }
}
