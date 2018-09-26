namespace FSharp.Configuration.TypeProviders

open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open FSharp.Quotations
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Configuration.Json
open Microsoft.Extensions.Configuration.Xml
open Microsoft.Extensions.Configuration.Ini
open System.Collections.Concurrent

[<assembly:CompilerServices.TypeProviderAssembly()>]
do ()

[<TypeProvider>]
type TypeProviders(config) as this = 
    inherit TypeProviderForNamespaces(config)
    
    let cache = ConcurrentDictionary()

    let getProviderType(typeName, assembly, nameSpace, resolutionFolder, createSource: unit -> #FileConfigurationSource) = 

        let providerType = ProvidedTypeDefinition(assembly, nameSpace, typeName, Some typeof<obj>, isErased = false)

        providerType.DefineStaticParameters(
            parameters = [ 
                ProvidedStaticParameter("Path", typeof<string>) 
                ProvidedStaticParameter("InstanceTypes", typeof<bool>, false) 
            ],             
            instantiationFunction = (fun typeName args ->
                cache.GetOrAdd(
                    typeName, 
                    valueFactory = fun _ -> 
                        let path = unbox args.[0]
                        let generateInstanceTypes = unbox args.[1]

                        let configRoot = 
                            ConfigurationBuilder()
                                .SetBasePath(resolutionFolder)
                                .Add(
                                    let source = createSource() 
                                    source.Path <- path
                                    source
                                )
                                .Build()
            
                        let tempAssembly = ProvidedAssembly()

                        let rootType = ProvidedTypeDefinition(tempAssembly, nameSpace, typeName, Some typeof<obj>, isErased = false)
                        //rootType.AddMember( ProvidedConstructor([], (fun _ -> <@@ () @@>), IsImplicitConstructor = true))

                        tempAssembly.AddTypes [ rootType ]

                        let addInstanceType (pt: ProvidedTypeDefinition) = 
                            if generateInstanceTypes
                            then 
                                let intanceType = ProvidedTypeDefinition("InstanceType", Some typeof<obj>, isErased = false)
                                let ctor = ProvidedConstructor([], (fun _ -> <@@ () @@>), IsImplicitConstructor = true) 

                                intanceType.AddMember ctor
                                pt.AddMember intanceType
                                intanceType
                            else    
                                Unchecked.defaultof<_>

                        let intanceType = addInstanceType rootType

                        let rec addChildSectionTypes (parentConfigSection: IConfiguration) (parentStaticType: ProvidedTypeDefinition) (parentInstanceType: ProvidedTypeDefinition) = 
                            for section in parentConfigSection.GetChildren() do
                                let sectionStaticType = ProvidedTypeDefinition(section.Key, Some typeof<obj>, isErased = false)
                                sectionStaticType.AddMember <| ProvidedField.Literal("Path", typeof<string>, section.Path) 
                                parentStaticType.AddMember sectionStaticType

                                let propType = 
                                    let isLeaf = section.Value <> null 
                                    if isLeaf
                                    then 
                                        sectionStaticType.AddMember( ProvidedField.Literal( "Value", typeof<string>, section.Value))
                                        typeof<string> 
                                    else
                                        let instanceType = addInstanceType sectionStaticType 
                                        addChildSectionTypes section sectionStaticType instanceType
                                        upcast instanceType

                                if generateInstanceTypes
                                then
                                    let backingField = ProvidedField(section.Key, propType)
                                    parentInstanceType.AddMember backingField
                                    let prop = 
                                        ProvidedProperty(
                                            section.Key, 
                                            propType, 
                                            getterCode = (fun args -> Expr.FieldGet(args.[0], backingField)), 
                                            setterCode = (fun args -> Expr.FieldSet(args.[0], backingField, args.[1]))
                                        )

                                    parentInstanceType.AddMember prop

                        addChildSectionTypes configRoot rootType intanceType
            

                        rootType
                )
            ) 
        )

        providerType
    
    do 
        let assembly = Assembly.GetExecutingAssembly()
        let nameSpace = this.GetType().Namespace
        
        this.AddNamespace(
            nameSpace, [ 
                getProviderType("JsonConfiguration", assembly, nameSpace, config.ResolutionFolder, JsonConfigurationSource)
                //getProviderType("XmlConfiguration", assembly, nameSpace, config.ResolutionFolder, XmlConfigurationSource)
                //getProviderType("IniConfiguration", assembly, nameSpace, config.ResolutionFolder, IniConfigurationSource)
            ]
        )

