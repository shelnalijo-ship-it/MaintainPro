import { ServiceDetail } from "@/features/external-services/service-detail";
export default async function ExternalServiceDetailPage({params}:{params:Promise<{id:string}>}){const {id}=await params;return <ServiceDetail id={id}/>}
