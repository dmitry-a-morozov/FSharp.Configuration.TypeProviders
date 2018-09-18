namespace FSharp.Configuration.TypeProviders

open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open FSharp.Quotations
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Configuration.Json
open Microsoft.Extensions.Configuration.Xml
open Microsoft.Extensions.Configuration.Ini

[<assembly:CompilerServices.TypeProviderAssembly()>]
do ()

[<TypeProvider>]
type TypeProviders(config) as this = 
    inherit TypeProviderForNamespaces(config)
    
#if !NO_GENERATIVE
    let tempAssembly = ProvidedAssembly()
    let isErased = Some false
#else
    let isErased = Some true
#endif

    let getProviderType(name, assembly, nameSpace, resolutionFolder, createSource: unit -> #FileConfigurationSource) = 

        let providerType = ProvidedTypeDefinition(assembly, nameSpace, name, Some typeof<obj>)

        providerType.DefineStaticParameters(
            parameters = [ ProvidedStaticParameter("Path", typeof<string>) ],             
            instantiationFunction = (fun typeName args ->
                let configRoot = 
                    ConfigurationBuilder()
                        .SetBasePath(resolutionFolder)
                        .Add(
                            let source = createSource() 
                            source.Path <- string args.[0]
                            source
                        )
                        .Build()
            
                let rootType = ProvidedTypeDefinition(assembly, nameSpace, typeName, Some typeof<obj>, ?isErased = isErased)
#if !NO_GENERATIVE
                tempAssembly.AddTypes [ rootType ]

                let addInstanceTypePrefix (pt: ProvidedTypeDefinition) = 
                    let intanceType = ProvidedTypeDefinition("InstanceType", Some typeof<obj>, ?isErased = isErased)
                    intanceType.AddMember <| ProvidedConstructor([], (fun _ -> <@@ () @@>), IsImplicitConstructor = true) 
                    pt.AddMember intanceType
                    intanceType

                let intanceType = addInstanceTypePrefix rootType
#else
                let intanceType = Unchecked.defaultof<_>
#endif
                let rec addChildSectionTypes (parentConfigSection: IConfiguration) (parentStaticType: ProvidedTypeDefinition) (parentInstanceType: ProvidedTypeDefinition) = 
                    for section in parentConfigSection.GetChildren() do
                        let sectionStaticType = ProvidedTypeDefinition(section.Key, Some typeof<obj>, ?isErased = isErased)
                        sectionStaticType.AddMember <| ProvidedField.Literal("Path", typeof<string>, section.Path) 
                        parentStaticType.AddMember sectionStaticType

                        let isSection = section.Value <> null 
                        if isSection
                        then 
                            sectionStaticType.AddMember( ProvidedField.Literal( "Value", typeof<string>, section.Value))
                        else //value
                            addChildSectionTypes section sectionStaticType parentInstanceType

                        //let propType = 
                        //    if section.Value <> null 
                        //    then 
                        //        sectionStaticType.AddMember( ProvidedField.Literal( "Value", typeof<string>, section.Value))
                        //        typeof<string> 
                        //    else    
                        //        let instanceType = addInstanceTypePrefix sectionStaticType
                        //        addChildSectionTypes section sectionStaticType instanceType
                        //        upcast instanceType

                        //let backingField = ProvidedField(section.Key, propType)
                        //parentInstanceType.AddMember backingField
                                
                        //parentInstanceType.AddMember <| 
                        //    ProvidedProperty(
                        //        section.Key, 
                        //        propType, 
                        //        getterCode = (fun args -> Expr.FieldGet(args.[0], backingField)), 
                        //        setterCode = (fun args -> Expr.FieldSet(args.[0], backingField, args.[1]))
                        //    )

                addChildSectionTypes configRoot rootType Unchecked.defaultof<_>
            

                rootType
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

