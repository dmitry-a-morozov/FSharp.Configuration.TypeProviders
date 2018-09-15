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

            
                let tempAssembly = ProvidedAssembly()
                let rootType = ProvidedTypeDefinition(tempAssembly, nameSpace, typeName, Some typeof<obj>, isErased = false)

                let addInstanceTypePrefix (pt: ProvidedTypeDefinition) = 
                    let intanceType = ProvidedTypeDefinition("InstanceType", Some typeof<obj>, isErased = false)
                    //let intanceType = ProvidedTypeDefinition(tempAssembly, nameSpace, "InstanceType", Some typeof<obj>, isErased = false)
                    intanceType.AddMember <| ProvidedConstructor([], (fun _ -> <@@ () @@>), IsImplicitConstructor = true) 
                    pt.AddMember intanceType
                    tempAssembly.AddTypes [ intanceType ]
                    intanceType

                let intanceType = addInstanceTypePrefix rootType

                let rec addChildSectionTypes (parentConfigSection: IConfiguration) (parentStaticType: ProvidedTypeDefinition) (parentInstanceType: ProvidedTypeDefinition option) = 
                    for section in parentConfigSection.GetChildren() do
                        let sectionStaticType = ProvidedTypeDefinition(section.Key, Some typeof<obj>, isErased = false)
                        sectionStaticType.AddMember <| ProvidedField.Literal("Path", typeof<string>, section.Path) 
                        parentStaticType.AddMember sectionStaticType

                        if section.Value <> null 
                        then 
                            sectionStaticType.AddMember <| ProvidedField.Literal("Value", typeof<string>, section.Value) 
                        else
                            addChildSectionTypes section sectionStaticType parentInstanceType

                        let propType = 
                            if section.Value <> null 
                            then 
                                sectionStaticType.AddMember <| ProvidedField.Literal("Value", typeof<string>, section.Value) 
                                typeof<string> 
                            else    
                                let instanceType = addInstanceTypePrefix sectionStaticType
                                addChildSectionTypes section sectionStaticType (Some instanceType)
                                upcast instanceType

                        if parentInstanceType.IsSome
                        then 
                            let backingField = ProvidedField(section.Key, propType)
                            parentInstanceType.Value.AddMember backingField
                                
                            parentInstanceType.Value.AddMember <| 
                                ProvidedProperty(
                                    section.Key, 
                                    propType, 
                                    getterCode = (fun args -> Expr.FieldGet(args.[0], backingField)), 
                                    setterCode = (fun args -> Expr.FieldSet(args.[0], backingField, args.[1]))
                                )

                //addChildSectionTypes configRoot rootType intanceType
                addChildSectionTypes configRoot rootType None

                tempAssembly.AddTypes [ rootType ]
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

